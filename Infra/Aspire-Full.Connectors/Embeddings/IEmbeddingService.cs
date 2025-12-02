namespace Aspire_Full.Connectors.Embeddings;

/// <summary>
/// Service for generating text embeddings using various providers.
/// Supports multiple embedding providers (OpenAI, Azure OpenAI, local models).
/// Optimized for GPU-accelerated workloads with SIMD support.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embeddings for a single text input.
    /// </summary>
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple text inputs (batch processing for GPU efficiency).
    /// </summary>
    Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings as async enumerable for streaming large batches.
    /// </summary>
    IAsyncEnumerable<ReadOnlyMemory<float>> GenerateEmbeddingsStreamAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the dimension size of the embedding model.
    /// </summary>
    int EmbeddingDimensions { get; }
}
