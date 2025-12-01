using Aspire_Full.Tensor.Native;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Tensor.Services;

public interface ITensorComputeService
{
    void MatrixMultiply(float[] a, float[] b, float[] result, int m, int n, int k);
    void MeanPooling(float[] input, long[] attentionMask, float[] output, int batchSize, int seqLen, int hiddenSize);
    void ReluActivation(float[] input, float[] output);
}

public class TensorComputeService : ITensorComputeService
{
    private readonly ILogger<TensorComputeService> _logger;

    public TensorComputeService(ILogger<TensorComputeService> logger)
    {
        _logger = logger;
    }

    public void MatrixMultiply(float[] a, float[] b, float[] result, int m, int n, int k)
    {
        var metrics = new TensorMetrics();
        NativeMethods.MatrixMultiply(a, b, result, m, n, k, ref metrics);
        LogMetrics("MatrixMultiply", metrics);
    }

    public void MeanPooling(float[] input, long[] attentionMask, float[] output, int batchSize, int seqLen, int hiddenSize)
    {
        var metrics = new TensorMetrics();
        NativeMethods.MeanPooling(input, attentionMask, output, batchSize, seqLen, hiddenSize, ref metrics);
        LogMetrics("MeanPooling", metrics);
    }

    public void ReluActivation(float[] input, float[] output)
    {
        var metrics = new TensorMetrics();
        NativeMethods.ReluActivation(input, output, input.Length, ref metrics);
        LogMetrics("ReluActivation", metrics);
    }

    private void LogMetrics(string operation, TensorMetrics metrics)
    {
        _logger.LogDebug("{Operation} completed in {Time}ms. Memory: {Memory}MB. Kernels: {Kernels}",
            operation, metrics.ComputeTimeMs, metrics.MemoryUsageMb, metrics.ActiveKernels);
    }
}
