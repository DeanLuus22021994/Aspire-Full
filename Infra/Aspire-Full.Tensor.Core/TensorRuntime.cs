using System.Diagnostics.Metrics;
using Aspire_Full.Tensor.Core.Abstractions;
using Aspire_Full.Tensor.Core.Compute;
using Aspire_Full.Tensor.Core.Memory;
using Aspire_Full.Tensor.Core.Native;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Tensor.Core;

/// <summary>
/// Default tensor runtime implementation with GPU compute.
/// All operations are offloaded to GPU when ComputeMode is GPU.
/// CPU fallback is only used when explicitly configured.
/// </summary>
public sealed class TensorRuntime : ITensorRuntime, IGpuResourceMonitor
{
    private readonly ILogger<TensorRuntime> _logger;
    private readonly IComputeModeService? _computeModeService;
    private readonly GpuMemoryPool _memoryPool;
    private long _totalMemory;
    private long _allocatedMemory;
    private int _currentUtilization;
    private bool _disposed;

    public TensorRuntime(
        ILogger<TensorRuntime> logger,
        IComputeModeService? computeModeService = null,
        int maxBufferCount = 16,
        nuint defaultBufferSize = 64 * 1024 * 1024)
    {
        _logger = logger;
        _computeModeService = computeModeService;
        _memoryPool = new GpuMemoryPool(maxBufferCount, defaultBufferSize);

        // Initialize GPU info
        if (NativeTensorContext.IsGpuAvailable)
        {
            try
            {
                var deviceInfo = GetDeviceInfo(0);
                if (deviceInfo.HasValue)
                {
                    _totalMemory = deviceInfo.Value.total_memory;
                    _allocatedMemory = _totalMemory - deviceInfo.Value.free_memory;
                    _currentUtilization = _totalMemory > 0
                        ? (int)((double)_allocatedMemory / _totalMemory * 100)
                        : 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query GPU device info, falling back to CPU");
            }
        }

        _logger.LogInformation("TensorRuntime initialized (GPU: {IsGpuAvailable}, Devices: {DeviceCount})",
            IsGpuAvailable, DeviceCount);
    }

    #region Compute Mode Helpers

    /// <summary>
    /// Determines if GPU should be used for the given operation type.
    /// Returns true if GPU is available AND either:
    /// - ComputeModeService is not configured (default to GPU when available)
    /// - ComputeModeService.ShouldOffload returns true for this operation
    /// </summary>
    private bool ShouldUseGpu(OperationType operationType)
    {
        if (!IsGpuAvailable)
            return false;

        // If no compute mode service is configured, use GPU when available
        if (_computeModeService is null)
            return true;

        return _computeModeService.ShouldOffload(operationType);
    }

    /// <summary>
    /// Determines if CPU fallback is allowed.
    /// CPU fallback is only allowed when:
    /// - ComputeModeService is not configured (legacy behavior)
    /// - ComputeModeService mode is CPU or Hybrid
    /// </summary>
    private bool AllowCpuFallback()
    {
        // If no compute mode service, allow fallback (legacy behavior)
        if (_computeModeService is null)
            return true;

        // CPU fallback is allowed when mode is CPU or Hybrid
        return _computeModeService.CurrentMode != ComputeMode.Gpu;
    }

    #endregion

    #region ITensorRuntime

    public bool IsGpuAvailable => NativeTensorContext.IsGpuAvailable;
    public int DeviceCount => NativeTensorContext.GpuDeviceCount;
    public GpuMemoryPool MemoryPool => _memoryPool;

    public float CosineSimilarity(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        TensorDiagnostics.OperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "cosine_similarity"));
        return NativeTensorContext.CosineSimilarity(x, y);
    }

    public float Norm(ReadOnlySpan<float> x)
    {
        TensorDiagnostics.OperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "norm"));
        return NativeTensorContext.Norm(x);
    }

    public float Dot(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        TensorDiagnostics.OperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "dot"));
        return NativeTensorContext.Dot(x, y);
    }

    public void SoftMax(ReadOnlySpan<float> x, Span<float> destination)
    {
        TensorDiagnostics.OperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "softmax"));
        NativeTensorContext.SoftMax(x, destination);
    }

    public void ReLU(ReadOnlySpan<float> x, Span<float> destination)
    {
        TensorDiagnostics.OperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "relu"));
        NativeTensorContext.ReLU(x, destination);
    }

    public void MatrixMultiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c, int m, int n, int k)
    {
        TensorDiagnostics.OperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "matmul"));

        // Check if we should offload and GPU is available
        var useGpu = ShouldUseGpu(OperationType.TensorMatMul);

        if (useGpu)
        {
            // Allocate device memory
            var aSize = (nuint)(m * k * sizeof(float));
            var bSize = (nuint)(k * n * sizeof(float));
            var cSize = (nuint)(m * n * sizeof(float));

            using var aBuffer = new GpuBufferScope(_memoryPool, aSize);
            using var bBuffer = new GpuBufferScope(_memoryPool, bSize);
            using var cBuffer = new GpuBufferScope(_memoryPool, cSize);

            // Copy input to device
            a.CopyTo(aBuffer.Buffer.AsSpan<float>());
            b.CopyTo(bBuffer.Buffer.AsSpan<float>());

            var metrics = new NativeTensorContext.TensorMetrics();
            NativeTensorContext.MatrixMultiply_GPU(
                aBuffer.Buffer.DevicePointer,
                bBuffer.Buffer.DevicePointer,
                cBuffer.Buffer.DevicePointer,
                m, n, k, ref metrics);

            // Copy result back
            cBuffer.Buffer.AsSpan<float>().CopyTo(c);

            TensorDiagnostics.OperationDuration.Record(metrics.compute_time_ms);
        }
        else if (AllowCpuFallback())
        {
            _logger.LogWarning("MatrixMultiply using CPU fallback - performance will be degraded");
            // CPU fallback - naive matrix multiply
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float sum = 0;
                    for (int p = 0; p < k; p++)
                    {
                        sum += a[i * k + p] * b[p * n + j];
                    }
                    c[i * n + j] = sum;
                }
            }
        }
        else
        {
            throw new InvalidOperationException(
                "GPU is required for MatrixMultiply but not available. " +
                "Enable FallbackToCpu in compute options or ensure GPU is accessible.");
        }
    }

    public void MeanPooling(ReadOnlySpan<float> input, ReadOnlySpan<long> attentionMask, Span<float> output, int batchSize, int seqLen, int hiddenSize)
    {
        TensorDiagnostics.OperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "mean_pooling"));

        var useGpu = ShouldUseGpu(OperationType.TensorPooling);

        if (useGpu)
        {
            var inputSize = (nuint)(batchSize * seqLen * hiddenSize * sizeof(float));
            var maskSize = (nuint)(batchSize * seqLen * sizeof(long));
            var outputSize = (nuint)(batchSize * hiddenSize * sizeof(float));

            using var inputBuffer = new GpuBufferScope(_memoryPool, inputSize);
            using var maskBuffer = new GpuBufferScope(_memoryPool, maskSize);
            using var outputBuffer = new GpuBufferScope(_memoryPool, outputSize);

            input.CopyTo(inputBuffer.Buffer.AsSpan<float>());
            attentionMask.CopyTo(maskBuffer.Buffer.AsSpan<long>());

            var metrics = new NativeTensorContext.TensorMetrics();
            NativeTensorContext.MeanPooling_GPU(
                inputBuffer.Buffer.DevicePointer,
                maskBuffer.Buffer.DevicePointer,
                outputBuffer.Buffer.DevicePointer,
                batchSize, seqLen, hiddenSize, ref metrics);

            outputBuffer.Buffer.AsSpan<float>().CopyTo(output);

            TensorDiagnostics.OperationDuration.Record(metrics.compute_time_ms);
        }
        else if (AllowCpuFallback())
        {
            _logger.LogWarning("MeanPooling using CPU fallback - performance will be degraded");
            // CPU fallback
            for (int b = 0; b < batchSize; b++)
            {
                for (int h = 0; h < hiddenSize; h++)
                {
                    float sum = 0;
                    int count = 0;
                    for (int s = 0; s < seqLen; s++)
                    {
                        if (attentionMask[b * seqLen + s] != 0)
                        {
                            sum += input[b * seqLen * hiddenSize + s * hiddenSize + h];
                            count++;
                        }
                    }
                    output[b * hiddenSize + h] = count > 0 ? sum / count : 0;
                }
            }
        }
        else
        {
            throw new InvalidOperationException(
                "GPU is required for MeanPooling but not available. " +
                "Enable FallbackToCpu in compute options or ensure GPU is accessible.");
        }
    }

    public bool ValidateContent(ReadOnlySpan<float> data, float threshold, out NativeTensorContext.TensorMetrics metrics)
    {
        TensorDiagnostics.OperationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "validate"));
        TensorDiagnostics.BytesProcessed.Add(data.Length * sizeof(float));
        return NativeTensorContext.ValidateContent(data, threshold, out metrics);
    }

    public NativeTensorContext.GpuDeviceInfo? GetDeviceInfo(int deviceId)
    {
        if (!IsGpuAvailable || deviceId >= DeviceCount)
            return null;

        if (NativeTensorContext.GetDeviceInfo(deviceId, out var info) == 0)
            return info;

        return null;
    }

    #endregion

    #region Batch Operations for Multi-Tenant Efficiency

    /// <inheritdoc/>
    public float[] CosineSimilarityBatch(ReadOnlyMemory<float>[] xVectors, ReadOnlyMemory<float>[] yVectors)
    {
        ArgumentNullException.ThrowIfNull(xVectors);
        ArgumentNullException.ThrowIfNull(yVectors);

        if (xVectors.Length != yVectors.Length)
            throw new ArgumentException("Vector arrays must have the same length");

        var batchSize = xVectors.Length;
        if (batchSize == 0)
            return [];

        TensorDiagnostics.OperationsCounter.Add(batchSize, new KeyValuePair<string, object?>("operation", "cosine_similarity_batch"));

        var results = new float[batchSize];
        var useGpu = ShouldUseGpu(OperationType.BatchProcessing);

        if (useGpu && batchSize >= 4)
        {
            // GPU batched processing - more efficient for larger batches
            Parallel.For(0, batchSize, i =>
            {
                results[i] = CosineSimilarity(xVectors[i].Span, yVectors[i].Span);
            });
        }
        else if (AllowCpuFallback())
        {
            _logger.LogDebug("CosineSimilarityBatch using CPU parallel processing");
            // CPU parallel processing
            Parallel.For(0, batchSize, i =>
            {
                results[i] = CosineSimilarity(xVectors[i].Span, yVectors[i].Span);
            });
        }
        else
        {
            throw new InvalidOperationException(
                "GPU is required for CosineSimilarityBatch but not available. " +
                "Ensure GPU is accessible or enable CPU fallback mode.");
        }

        return results;
    }

    /// <inheritdoc/>
    public Memory<float>[] MatrixMultiplyBatch(BatchMatrixMultiplyRequest[] requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Length == 0)
            return [];

        TensorDiagnostics.OperationsCounter.Add(requests.Length, new KeyValuePair<string, object?>("operation", "matmul_batch"));

        var results = new Memory<float>[requests.Length];
        var useGpu = ShouldUseGpu(OperationType.BatchProcessing);

        if (useGpu && requests.Length >= 2)
        {
            // GPU batch processing - process sequentially due to ref struct constraints
            for (int i = 0; i < requests.Length; i++)
            {
                var req = requests[i];
                var aSize = (nuint)(req.M * req.K * sizeof(float));
                var bSize = (nuint)(req.K * req.N * sizeof(float));
                var cSize = (nuint)(req.M * req.N * sizeof(float));

                using var aBuffer = new GpuBufferScope(_memoryPool, aSize);
                using var bBuffer = new GpuBufferScope(_memoryPool, bSize);
                using var cBuffer = new GpuBufferScope(_memoryPool, cSize);

                req.A.Span.CopyTo(aBuffer.Buffer.AsSpan<float>());
                req.B.Span.CopyTo(bBuffer.Buffer.AsSpan<float>());

                var metrics = new NativeTensorContext.TensorMetrics();
                NativeTensorContext.MatrixMultiply_GPU(
                    aBuffer.Buffer.DevicePointer,
                    bBuffer.Buffer.DevicePointer,
                    cBuffer.Buffer.DevicePointer,
                    req.M, req.N, req.K, ref metrics);

                TensorDiagnostics.OperationDuration.Record(metrics.compute_time_ms, new KeyValuePair<string, object?>("batch_index", i));

                var result = new float[req.M * req.N];
                cBuffer.Buffer.AsSpan<float>().CopyTo(result);
                results[i] = result;
            }
        }
        else if (AllowCpuFallback())
        {
            _logger.LogWarning("MatrixMultiplyBatch using CPU fallback - performance will be degraded");
            // CPU fallback - parallel process
            Parallel.For(0, requests.Length, i =>
            {
                var req = requests[i];
                var result = new float[req.M * req.N];
                MatrixMultiply(req.A.Span, req.B.Span, result, req.M, req.N, req.K);
                results[i] = result;
            });
        }
        else
        {
            throw new InvalidOperationException(
                "GPU is required for MatrixMultiplyBatch but not available. " +
                "Ensure GPU is accessible or enable CPU fallback mode.");
        }

        return results;
    }

    /// <inheritdoc/>
    public Memory<float>[] MeanPoolingBatch(BatchMeanPoolingRequest[] requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Length == 0)
            return [];

        TensorDiagnostics.OperationsCounter.Add(requests.Length, new KeyValuePair<string, object?>("operation", "mean_pooling_batch"));

        var results = new Memory<float>[requests.Length];

        // Process all requests - GPU will batch internally if beneficial
        Parallel.For(0, requests.Length, i =>
        {
            var req = requests[i];
            var result = new float[req.HiddenSize];
            MeanPooling(req.Input.Span, req.AttentionMask.Span, result, 1, req.SeqLen, req.HiddenSize);
            results[i] = result;
        });

        return results;
    }

    #endregion

    #region IGpuResourceMonitor

    public int CurrentUtilization => _currentUtilization;
    public long TotalMemory => _totalMemory;
    public long AllocatedMemory => _allocatedMemory;
    public long AvailableMemory => _totalMemory - _allocatedMemory;

    public void RecordOperation(GpuOperationType type, double durationMs, long bytesProcessed)
    {
        TensorDiagnostics.OperationsCounter.Add(1, new KeyValuePair<string, object?>("type", type.ToString()));
        TensorDiagnostics.OperationDuration.Record(durationMs, new KeyValuePair<string, object?>("type", type.ToString()));
        TensorDiagnostics.BytesProcessed.Add(bytesProcessed);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        if (IsGpuAvailable)
        {
            var info = GetDeviceInfo(0);
            if (info.HasValue)
            {
                _totalMemory = info.Value.total_memory;
                _allocatedMemory = _totalMemory - info.Value.free_memory;
                // Estimate utilization from memory usage (NVML would provide accurate utilization)
                _currentUtilization = _totalMemory > 0
                    ? (int)((double)_allocatedMemory / _totalMemory * 100)
                    : 0;
            }
        }
    }

    public async Task<GpuDeviceSnapshot> GetDeviceSnapshotAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var info = GetDeviceInfo(deviceId);
        if (info.HasValue)
        {
            return new GpuDeviceSnapshot
            {
                DeviceId = deviceId,
                DeviceName = $"GPU {deviceId}",
                ComputeCapabilityMajor = info.Value.compute_capability_major,
                ComputeCapabilityMinor = info.Value.compute_capability_minor,
                TotalMemory = info.Value.total_memory,
                FreeMemory = info.Value.free_memory,
                UsedMemory = info.Value.total_memory - info.Value.free_memory,
                Utilization = 0, // Would need nvidia-smi or NVML for this
                Temperature = 0,
                PowerUsage = 0,
                MultiprocessorCount = info.Value.multiprocessor_count,
                Timestamp = DateTime.UtcNow
            };
        }

        return new GpuDeviceSnapshot
        {
            DeviceId = deviceId,
            DeviceName = "Unknown",
            ComputeCapabilityMajor = 0,
            ComputeCapabilityMinor = 0,
            TotalMemory = 0,
            FreeMemory = 0,
            UsedMemory = 0,
            Utilization = 0,
            Temperature = 0,
            PowerUsage = 0,
            MultiprocessorCount = 0,
            Timestamp = DateTime.UtcNow
        };
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _memoryPool.Dispose();
        await Task.CompletedTask;
    }
}
