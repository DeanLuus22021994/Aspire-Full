using Aspire_Full.Shared;
using Aspire_Full.Tensor.Native;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Tensor.Services;

public interface ITensorComputeService
{
    Result MatrixMultiply(float[] a, float[] b, float[] result, int m, int n, int k);
    Result MeanPooling(float[] input, long[] attentionMask, float[] output, int batchSize, int seqLen, int hiddenSize);
    Result ReluActivation(float[] input, float[] output);
}

public class TensorComputeService : ITensorComputeService
{
    private readonly ILogger<TensorComputeService> _logger;

    public TensorComputeService(ILogger<TensorComputeService> logger)
    {
        _logger = logger;
    }

    public Result MatrixMultiply(float[] a, float[] b, float[] result, int m, int n, int k)
    {
        try
        {
            using var d_A = new GpuTensor<float>(m * k);
            using var d_B = new GpuTensor<float>(k * n);
            using var d_C = new GpuTensor<float>(m * n);

            d_A.Upload(a);
            d_B.Upload(b);

            var metrics = new TensorMetrics();
            NativeMethods.MatrixMultiply_GPU(d_A.Pointer, d_B.Pointer, d_C.Pointer, m, n, k, ref metrics);

            d_C.Download(result);
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
            using var d_Input = new GpuTensor<float>(batchSize * seqLen * hiddenSize);
            using var d_Mask = new GpuTensor<long>(batchSize * seqLen);
            using var d_Output = new GpuTensor<float>(batchSize * hiddenSize);

            d_Input.Upload(input);
            d_Mask.Upload(attentionMask);

            var metrics = new TensorMetrics();
            NativeMethods.MeanPooling_GPU(d_Input.Pointer, d_Mask.Pointer, d_Output.Pointer, batchSize, seqLen, hiddenSize, ref metrics);

            d_Output.Download(output);
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
            using var d_Input = new GpuTensor<float>(input.Length);
            using var d_Output = new GpuTensor<float>(output.Length);

            d_Input.Upload(input);

            var metrics = new TensorMetrics();
            NativeMethods.ReluActivation_GPU(d_Input.Pointer, d_Output.Pointer, input.Length, ref metrics);

            d_Output.Download(output);
            LogMetrics("ReluActivation", metrics);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReluActivation failed");
            return Result.Failure(ex.Message);
        }
    }

    private void LogMetrics(string operation, TensorMetrics metrics)
    {
        _logger.LogDebug("{Operation} completed in {Time}ms. Memory: {Memory}MB. Kernels: {Kernels}",
            operation, metrics.ComputeTimeMs, metrics.MemoryUsageMb, metrics.ActiveKernels);
    }
}
