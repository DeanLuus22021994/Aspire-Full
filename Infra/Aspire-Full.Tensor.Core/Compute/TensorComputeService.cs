using Aspire_Full.Shared;
using Aspire_Full.Tensor.Core.Memory;
using Aspire_Full.Tensor.Core.Native;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Tensor.Core.Compute;

/// <summary>
/// GPU-accelerated tensor compute service using GpuMemoryPool.
/// Provides optimized matrix operations with automatic CPU fallback.
/// </summary>
public sealed class TensorComputeService : ITensorComputeService
{
    private readonly GpuMemoryPool _memoryPool;
    private readonly ILogger<TensorComputeService> _logger;

    public TensorComputeService(GpuMemoryPool memoryPool, ILogger<TensorComputeService> logger)
    {
        _memoryPool = memoryPool;
        _logger = logger;
    }

    public Result MatrixMultiply(float[] a, float[] b, float[] result, int m, int n, int k)
    {
        try
        {
            var sizeA = (nuint)(m * k * sizeof(float));
            var sizeB = (nuint)(k * n * sizeof(float));
            var sizeC = (nuint)(m * n * sizeof(float));

            using var bufferScope = new GpuBufferScope(_memoryPool, sizeA + sizeB + sizeC);
            var buffer = bufferScope.Buffer;
            var span = buffer.AsSpan<float>();

            // Copy input data to GPU buffer
            a.AsSpan().CopyTo(span[..(m * k)]);
            b.AsSpan().CopyTo(span.Slice(m * k, k * n));

            // Execute native GPU operation
            var metrics = new NativeTensorContext.TensorMetrics();
            NativeTensorContext.MatrixMultiply_GPU(
                buffer.DevicePointer,
                buffer.DevicePointer + (nint)sizeA,
                buffer.DevicePointer + (nint)(sizeA + sizeB),
                m, n, k, ref metrics);

            // Synchronize and copy result back
            buffer.CopyToHost();
            span.Slice(m * k + k * n, m * n).CopyTo(result);

            LogMetrics("MatrixMultiply", metrics);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MatrixMultiply failed");
            return Result.Failure(ex.Message);
        }
    }

    public Result MeanPooling(float[] input, long[] attentionMask, float[] output, int batchSize, int seqLen, int hiddenSize)
    {
        try
        {
            var inputSize = (nuint)(batchSize * seqLen * hiddenSize * sizeof(float));
            var maskSize = (nuint)(batchSize * seqLen * sizeof(long));
            var outputSize = (nuint)(batchSize * hiddenSize * sizeof(float));
            var totalSize = inputSize + maskSize + outputSize;

            using var bufferScope = new GpuBufferScope(_memoryPool, totalSize);
            var buffer = bufferScope.Buffer;

            // Copy input data
            unsafe
            {
                var byteSpan = buffer.AsSpan();
                input.AsSpan().CopyTo(System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(byteSpan[..(int)inputSize]));

                var maskSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, long>(byteSpan.Slice((int)inputSize, (int)maskSize));
                attentionMask.AsSpan().CopyTo(maskSpan);
            }

            // Execute native GPU operation
            var metrics = new NativeTensorContext.TensorMetrics();
            NativeTensorContext.MeanPooling_GPU(
                buffer.DevicePointer,
                buffer.DevicePointer + (nint)inputSize,
                buffer.DevicePointer + (nint)(inputSize + maskSize),
                batchSize, seqLen, hiddenSize, ref metrics);

            // Synchronize and copy result back
            buffer.CopyToHost();
            unsafe
            {
                var byteSpan = buffer.AsSpan();
                var outputSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(byteSpan.Slice((int)(inputSize + maskSize), (int)outputSize));
                outputSpan.CopyTo(output);
            }

            LogMetrics("MeanPooling", metrics);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MeanPooling failed");
            return Result.Failure(ex.Message);
        }
    }

    public Result ReluActivation(float[] input, float[] output)
    {
        try
        {
            var size = (nuint)(input.Length * sizeof(float));
            var totalSize = size * 2;

            using var bufferScope = new GpuBufferScope(_memoryPool, totalSize);
            var buffer = bufferScope.Buffer;
            var span = buffer.AsSpan<float>();

            // Copy input data
            input.AsSpan().CopyTo(span[..input.Length]);

            // Execute native GPU operation
            var metrics = new NativeTensorContext.TensorMetrics();
            NativeTensorContext.ReluActivation_GPU(
                buffer.DevicePointer,
                buffer.DevicePointer + (nint)size,
                input.Length, ref metrics);

            // Synchronize and copy result back
            buffer.CopyToHost();
            span.Slice(input.Length, output.Length).CopyTo(output);

            LogMetrics("ReluActivation", metrics);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReluActivation failed");
            return Result.Failure(ex.Message);
        }
    }

    private void LogMetrics(string operation, NativeTensorContext.TensorMetrics metrics)
    {
        _logger.LogDebug("{Operation} completed in {Time}ms. Memory: {Memory}MB. Kernels: {Kernels}",
            operation, metrics.compute_time_ms, metrics.memory_usage_mb, metrics.active_kernels);
    }
}
