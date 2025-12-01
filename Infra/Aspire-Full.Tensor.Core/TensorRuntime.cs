using System.Diagnostics.Metrics;
using Aspire_Full.Tensor.Core.Abstractions;
using Aspire_Full.Tensor.Core.Memory;
using Aspire_Full.Tensor.Core.Native;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Tensor.Core;

/// <summary>
/// Default tensor runtime implementation with GPU/CPU fallback.
/// Provides unified access to GPU compute operations via NativeTensorContext.
/// </summary>
public sealed class TensorRuntime : ITensorRuntime, IGpuResourceMonitor
{
    private readonly ILogger<TensorRuntime> _logger;
    private readonly GpuMemoryPool _memoryPool;
    private long _totalMemory;
    private long _allocatedMemory;
    private int _currentUtilization;
    private bool _disposed;

    // Metrics
    private static readonly Meter s_meter = new("Aspire.Tensor.Core.Runtime", "1.0.0");
    private static readonly Counter<long> s_operationsCounter = s_meter.CreateCounter<long>("tensor.operations.total");
    private static readonly Histogram<double> s_operationDuration = s_meter.CreateHistogram<double>("tensor.operation.duration_ms");
    private static readonly Counter<long> s_bytesProcessed = s_meter.CreateCounter<long>("tensor.bytes.processed");

    public TensorRuntime(ILogger<TensorRuntime> logger, int maxBufferCount = 16, nuint defaultBufferSize = 64 * 1024 * 1024)
    {
        _logger = logger;
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

    #region ITensorRuntime

    public bool IsGpuAvailable => NativeTensorContext.IsGpuAvailable;
    public int DeviceCount => NativeTensorContext.GpuDeviceCount;
    public GpuMemoryPool MemoryPool => _memoryPool;

    public float CosineSimilarity(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        s_operationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "cosine_similarity"));
        return NativeTensorContext.CosineSimilarity(x, y);
    }

    public float Norm(ReadOnlySpan<float> x)
    {
        s_operationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "norm"));
        return NativeTensorContext.Norm(x);
    }

    public float Dot(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        s_operationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "dot"));
        return NativeTensorContext.Dot(x, y);
    }

    public void SoftMax(ReadOnlySpan<float> x, Span<float> destination)
    {
        s_operationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "softmax"));
        NativeTensorContext.SoftMax(x, destination);
    }

    public void ReLU(ReadOnlySpan<float> x, Span<float> destination)
    {
        s_operationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "relu"));
        NativeTensorContext.ReLU(x, destination);
    }

    public void MatrixMultiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c, int m, int n, int k)
    {
        s_operationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "matmul"));

        if (IsGpuAvailable)
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

            s_operationDuration.Record(metrics.compute_time_ms);
        }
        else
        {
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
    }

    public void MeanPooling(ReadOnlySpan<float> input, ReadOnlySpan<long> attentionMask, Span<float> output, int batchSize, int seqLen, int hiddenSize)
    {
        s_operationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "mean_pooling"));

        if (IsGpuAvailable)
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

            s_operationDuration.Record(metrics.compute_time_ms);
        }
        else
        {
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
    }

    public bool ValidateContent(ReadOnlySpan<float> data, float threshold, out NativeTensorContext.TensorMetrics metrics)
    {
        s_operationsCounter.Add(1, new KeyValuePair<string, object?>("operation", "validate"));
        s_bytesProcessed.Add(data.Length * sizeof(float));
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

    #region IGpuResourceMonitor

    public int CurrentUtilization => _currentUtilization;
    public long TotalMemory => _totalMemory;
    public long AllocatedMemory => _allocatedMemory;
    public long AvailableMemory => _totalMemory - _allocatedMemory;

    public void RecordOperation(GpuOperationType type, double durationMs, long bytesProcessed)
    {
        s_operationsCounter.Add(1, new KeyValuePair<string, object?>("type", type.ToString()));
        s_operationDuration.Record(durationMs, new KeyValuePair<string, object?>("type", type.ToString()));
        s_bytesProcessed.Add(bytesProcessed);
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
