using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;

namespace Aspire_Full.VectorStore;

/// <summary>
/// Represents a document stored in the vector store with upsert/downsert support.
/// </summary>
public record VectorDocument
{
    [VectorStoreRecordKey]
    public required string Id { get; init; }

    [VectorStoreRecordData]
    public required string Content { get; init; }

    [VectorStoreRecordVector(1536, DistanceFunction.CosineSimilarity)]
    public required ReadOnlyMemory<float> Embedding { get; init; }

    [VectorStoreRecordData]
    public bool IsDeleted { get; init; } = false;

    [VectorStoreRecordData]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [VectorStoreRecordData]
    public DateTime? UpdatedAt { get; init; }

    [VectorStoreRecordData]
    public DateTime? DeletedAt { get; init; }
}

/// <summary>
/// Service for vector storage operations with upsert/downsert (soft-delete) support.
/// Uses Qdrant via Semantic Kernel abstractions for .NET 10 compatibility.
/// </summary>
public interface IVectorStoreService
{
    /// <summary>
    /// Upsert a document - creates if new, updates if exists, reactivates if soft-deleted.
    /// </summary>
    Task<VectorDocument> UpsertAsync(VectorDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downsert (soft-delete) a document - marks as deleted but retains data.
    /// </summary>
    Task<bool> DownsertAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard delete a document - permanently removes from store.
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar documents using vector similarity.
    /// </summary>
    Task<IList<VectorDocument>> SearchAsync(ReadOnlyMemory<float> queryVector, int topK = 10, bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a document by ID.
    /// </summary>
    Task<VectorDocument?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensure the collection exists with proper configuration.
    /// </summary>
    Task EnsureCollectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Qdrant implementation of vector store with upsert/downsert pattern using SK abstractions.
/// </summary>
public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly IVectorStoreRecordCollection<string, VectorDocument> _collection;
    private readonly ILogger<QdrantVectorStoreService> _logger;
    private readonly string _collectionName;

    public QdrantVectorStoreService(
        QdrantClient client,
        ILogger<QdrantVectorStoreService> logger,
        string collectionName = "documents")
    {
        ArgumentNullException.ThrowIfNull(client);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _collectionName = collectionName;

        var vectorStore = new QdrantVectorStore(client);
        _collection = vectorStore.GetCollection<string, VectorDocument>(collectionName);
    }

    public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
    {
        await _collection.CreateCollectionIfNotExistsAsync(cancellationToken);
        _logger.LogInformation("Ensured Qdrant collection exists: {CollectionName}", _collectionName);
    }

    public async Task<VectorDocument> UpsertAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var existingDoc = await GetAsync(document.Id, cancellationToken);
        var now = DateTime.UtcNow;

        var updatedDoc = document with
        {
            CreatedAt = existingDoc?.CreatedAt ?? now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        await _collection.UpsertAsync(updatedDoc, cancellationToken: cancellationToken);

        var action = existingDoc == null ? "Created" : (existingDoc.IsDeleted ? "Reactivated" : "Updated");
        _logger.LogInformation("{Action} document {Id} in collection {Collection}", action, document.Id, _collectionName);

        return updatedDoc;
    }

    public async Task<bool> DownsertAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var existing = await GetAsync(id, cancellationToken);
        if (existing == null) return false;

        var now = DateTime.UtcNow;
        var softDeleted = existing with
        {
            IsDeleted = true,
            DeletedAt = now,
            UpdatedAt = now
        };

        await _collection.UpsertAsync(softDeleted, cancellationToken: cancellationToken);
        _logger.LogInformation("Soft-deleted (downsert) document {Id} in collection {Collection}", id, _collectionName);
        return true;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await _collection.DeleteAsync(id, cancellationToken: cancellationToken);
        _logger.LogInformation("Hard-deleted document {Id} from collection {Collection}", id, _collectionName);
        return true;
    }

    public async Task<VectorDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return await _collection.GetAsync(id, cancellationToken: cancellationToken);
    }

    public async Task<IList<VectorDocument>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topK = 10,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var searchOptions = new VectorSearchOptions
        {
            Top = topK
        };

        // Note: Filtering for soft-deleted documents would need a filter expression
        // For now, we filter in-memory after search
        var results = await _collection.VectorizedSearchAsync(queryVector, searchOptions, cancellationToken);

        var documents = new List<VectorDocument>();
        await foreach (var result in results.Results.WithCancellation(cancellationToken))
        {
            if (result.Record != null && (includeDeleted || !result.Record.IsDeleted))
            {
                documents.Add(result.Record);
            }
        }

        return documents;
    }
}
