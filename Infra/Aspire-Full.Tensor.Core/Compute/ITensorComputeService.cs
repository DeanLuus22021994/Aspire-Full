using Aspire_Full.Shared;

namespace Aspire_Full.Tensor.Core.Compute;

/// <summary>
/// GPU-accelerated tensor compute operations with automatic CPU fallback.
/// </summary>
public interface ITensorComputeService
{
    /// <summary>
    /// Performs GPU matrix multiplication: C = A × B.
    /// </summary>
    /// <param name="a">Input matrix A (m × k).</param>
    /// <param name="b">Input matrix B (k × n).</param>
    /// <param name="result">Output matrix C (m × n).</param>
    /// <param name="m">Rows in A / result.</param>
    /// <param name="n">Columns in B / result.</param>
    /// <param name="k">Columns in A / rows in B.</param>
    /// <returns>Result indicating success or failure.</returns>
    Result MatrixMultiply(float[] a, float[] b, float[] result, int m, int n, int k);

    /// <summary>
    /// Performs mean pooling over sequence dimension with attention mask.
    /// </summary>
    /// <param name="input">Input tensor (batchSize × seqLen × hiddenSize).</param>
    /// <param name="attentionMask">Attention mask (batchSize × seqLen).</param>
    /// <param name="output">Output tensor (batchSize × hiddenSize).</param>
    /// <param name="batchSize">Number of sequences in batch.</param>
    /// <param name="seqLen">Sequence length.</param>
    /// <param name="hiddenSize">Hidden dimension size.</param>
    /// <returns>Result indicating success or failure.</returns>
    Result MeanPooling(float[] input, long[] attentionMask, float[] output, int batchSize, int seqLen, int hiddenSize);

    /// <summary>
    /// Applies ReLU activation function in-place.
    /// </summary>
    /// <param name="input">Input tensor.</param>
    /// <param name="output">Output tensor.</param>
    /// <returns>Result indicating success or failure.</returns>
    Result ReluActivation(float[] input, float[] output);
}
