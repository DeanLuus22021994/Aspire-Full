using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace Aspire_Full.Embeddings;

/// <summary>
/// Service for generating text embeddings using Semantic Kernel.
/// Supports multiple embedding providers (OpenAI, Azure OpenAI, local models).
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embeddings for a single text input.
    /// </summary>
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple text inputs (batch processing).
    /// </summary>
    Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the dimension size of the embedding model.
    /// </summary>
    int EmbeddingDimensions { get; }
}

/// <summary>
/// Default implementation of IEmbeddingService using Semantic Kernel.
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly int _dimensions;

    public EmbeddingService(
        ITextEmbeddingGenerationService embeddingService,
        ILogger<EmbeddingService> logger,
        int dimensions = 1536) // Default for text-embedding-ada-002
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dimensions = dimensions;
    }

    public int EmbeddingDimensions => _dimensions;

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);

        var embeddings = await _embeddingService.GenerateEmbeddingsAsync([text], null, cancellationToken);
        return embeddings[0];
    }

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> texts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
            return [];

        _logger.LogDebug("Generating embeddings for {Count} texts", texts.Count);

        return await _embeddingService.GenerateEmbeddingsAsync(texts, null, cancellationToken);
    }
}
