using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Embeddings;

/// <summary>
/// Service for generating text embeddings using Microsoft.Extensions.AI.
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

/// <summary>
/// Default implementation of IEmbeddingService using Microsoft.Extensions.AI.
/// Uses IEmbeddingGenerator for modern .NET 10 compatible embedding generation.
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly int _dimensions;

    public EmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<EmbeddingService> logger,
        int dimensions = 1536) // Default for text-embedding-ada-002
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dimensions = dimensions;

        _logger.LogInformation("EmbeddingService initialized with dimension size: {Dimensions}", _dimensions);
    }

    public int EmbeddingDimensions => _dimensions;

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);

        var embeddings = await _embeddingGenerator.GenerateAsync([text], cancellationToken: cancellationToken).ConfigureAwait(false);
        return embeddings[0].Vector;
    }

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> texts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
            return [];

        _logger.LogDebug("Generating embeddings for {Count} texts", texts.Count);

        var embeddings = await _embeddingGenerator.GenerateAsync(texts, cancellationToken: cancellationToken).ConfigureAwait(false);
        return embeddings.Select(e => e.Vector).ToList();
    }

    public async IAsyncEnumerable<ReadOnlyMemory<float>> GenerateEmbeddingsStreamAsync(
        IEnumerable<string> texts,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var batch = new List<string>();
        const int batchSize = 100; // Optimal batch size for GPU efficiency

        foreach (var text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(text);

            if (batch.Count >= batchSize)
            {
                var embeddings = await _embeddingGenerator.GenerateAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);
                foreach (var embedding in embeddings)
                {
                    yield return embedding.Vector;
                }
                batch.Clear();
            }
        }

        // Process remaining items
        if (batch.Count > 0)
        {
            var embeddings = await _embeddingGenerator.GenerateAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);
            foreach (var embedding in embeddings)
            {
                yield return embedding.Vector;
            }
        }
    }
}
