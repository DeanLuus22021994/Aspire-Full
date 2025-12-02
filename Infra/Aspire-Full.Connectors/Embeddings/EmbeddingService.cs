using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Connectors.Embeddings;

/// <summary>
/// Default implementation of <see cref="IEmbeddingService"/> using Microsoft.Extensions.AI.
/// Uses <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> for modern .NET 10 compatible embedding generation.
/// Optimized for GPU-accelerated workloads with batch processing.
/// </summary>
public sealed class EmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly int _dimensions;

    private const int DefaultBatchSize = 100; // Optimal batch size for GPU efficiency

    /// <summary>
    /// Initializes a new instance of <see cref="EmbeddingService"/>.
    /// </summary>
    /// <param name="embeddingGenerator">The embedding generator to use.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="dimensions">Embedding dimension size. Default is 1536 for text-embedding-ada-002.</param>
    public EmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<EmbeddingService> logger,
        int dimensions = 1536)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dimensions = dimensions;

        _logger.LogInformation("EmbeddingService initialized with dimension size: {Dimensions}", _dimensions);
    }

    /// <inheritdoc />
    public int EmbeddingDimensions => _dimensions;

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);

        var embeddings = await _embeddingGenerator
            .GenerateAsync([text], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return embeddings[0].Vector;
    }

    /// <inheritdoc />
    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> texts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
            return [];

        _logger.LogDebug("Generating embeddings for {Count} texts", texts.Count);

        var embeddings = await _embeddingGenerator
            .GenerateAsync(texts, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return embeddings.Select(e => e.Vector).ToList();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadOnlyMemory<float>> GenerateEmbeddingsStreamAsync(
        IEnumerable<string> texts,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var batch = new List<string>(DefaultBatchSize);

        foreach (var text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(text);

            if (batch.Count >= DefaultBatchSize)
            {
                var embeddings = await _embeddingGenerator
                    .GenerateAsync(batch, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

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
            var embeddings = await _embeddingGenerator
                .GenerateAsync(batch, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            foreach (var embedding in embeddings)
            {
                yield return embedding.Vector;
            }
        }
    }
}
