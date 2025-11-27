using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Aspire_Full.VectorStore;

/// <summary>
/// Represents a document stored in the vector store with upsert/downsert support.
/// </summary>
public record VectorDocument
{
    public required string Id { get; init; }
    public required string Content { get; init; }
    public required ReadOnlyMemory<float> Embedding { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public bool IsDeleted { get; init; } = false;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}

/// <summary>
/// Service for vector storage operations with upsert/downsert (soft-delete) support.
/// Uses Qdrant as the underlying vector database.
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
    Task EnsureCollectionAsync(string collectionName, int vectorSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// Qdrant implementation of vector store with upsert/downsert pattern.
/// </summary>
public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorStoreService> _logger;
    private readonly string _collectionName;
    private readonly int _vectorSize;

    public QdrantVectorStoreService(
        QdrantClient client,
        ILogger<QdrantVectorStoreService> logger,
        string collectionName = "documents",
        int vectorSize = 1536)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _collectionName = collectionName;
        _vectorSize = vectorSize;
    }

    public async Task EnsureCollectionAsync(string collectionName, int vectorSize, CancellationToken cancellationToken = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken);
        if (!collections.Contains(collectionName))
        {
            await _client.CreateCollectionAsync(
                collectionName,
                new VectorParams { Size = (ulong)vectorSize, Distance = Distance.Cosine },
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created Qdrant collection: {CollectionName}", collectionName);
        }
    }

    public async Task<VectorDocument> UpsertAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var existingDoc = await GetAsync(document.Id, cancellationToken);
        var now = DateTime.UtcNow;

        var payload = new Dictionary<string, Value>
        {
            ["content"] = document.Content,
            ["is_deleted"] = false,
            ["created_at"] = existingDoc?.CreatedAt.ToString("O") ?? now.ToString("O"),
            ["updated_at"] = now.ToString("O")
        };

        if (document.Metadata != null)
        {
            foreach (var kvp in document.Metadata)
            {
                payload[$"meta_{kvp.Key}"] = kvp.Value?.ToString() ?? string.Empty;
            }
        }

        var point = new PointStruct
        {
            Id = new PointId { Uuid = document.Id },
            Vectors = document.Embedding.ToArray(),
            Payload = { payload }
        };

        await _client.UpsertAsync(_collectionName, [point], cancellationToken: cancellationToken);

        var action = existingDoc == null ? "Created" : (existingDoc.IsDeleted ? "Reactivated" : "Updated");
        _logger.LogInformation("{Action} document {Id} in collection {Collection}", action, document.Id, _collectionName);

        return document with
        {
            CreatedAt = existingDoc?.CreatedAt ?? now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    public async Task<bool> DownsertAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var existing = await GetAsync(id, cancellationToken);
        if (existing == null) return false;

        // Re-upsert with soft-delete flag
        var now = DateTime.UtcNow;
        var softDeleted = existing with
        {
            IsDeleted = true,
            DeletedAt = now,
            UpdatedAt = now
        };

        await UpsertSoftDeleteAsync(softDeleted, cancellationToken);
        _logger.LogInformation("Soft-deleted (downsert) document {Id} in collection {Collection}", id, _collectionName);
        return true;
    }

    private async Task UpsertSoftDeleteAsync(VectorDocument document, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, Value>
        {
            ["content"] = document.Content,
            ["is_deleted"] = true,
            ["created_at"] = document.CreatedAt.ToString("O"),
            ["updated_at"] = document.UpdatedAt?.ToString("O") ?? DateTime.UtcNow.ToString("O"),
            ["deleted_at"] = document.DeletedAt?.ToString("O") ?? DateTime.UtcNow.ToString("O")
        };

        var point = new PointStruct
        {
            Id = new PointId { Uuid = document.Id },
            Vectors = document.Embedding.ToArray(),
            Payload = { payload }
        };

        await _client.UpsertAsync(_collectionName, [point], cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var pointsSelector = new PointsSelector
        {
            Points = new PointsIdsList { Ids = { new PointId { Uuid = id } } }
        };

        await _client.DeleteAsync(_collectionName, pointsSelector, cancellationToken: cancellationToken);
        _logger.LogInformation("Hard-deleted document {Id} from collection {Collection}", id, _collectionName);
        return true;
    }

    public async Task<VectorDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var points = await _client.RetrieveAsync(
                _collectionName,
                [new PointId { Uuid = id }],
                withPayload: true,
                withVectors: true,
                cancellationToken: cancellationToken);

            var point = points.FirstOrDefault();
            if (point == null) return null;

            return MapPointToDocument(point);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IList<VectorDocument>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topK = 10,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        Filter? filter = includeDeleted ? null : new Filter
        {
            Must = {
                new Condition { Field = new FieldCondition {
                    Key = "is_deleted",
                    Match = new Match { Boolean = false }
                }}
            }
        };

        var results = await _client.SearchAsync(
            _collectionName,
            queryVector.ToArray(),
            filter: filter,
            limit: (ulong)topK,
            payloadSelector: true,
            vectorsSelector: true,
            cancellationToken: cancellationToken);

        return results.Select(MapScoredPointToDocument).ToList();
    }

    private static VectorDocument MapPointToDocument(RetrievedPoint point)
    {
        var payload = point.Payload;
        return new VectorDocument
        {
            Id = point.Id.Uuid,
            Content = payload.TryGetValue("content", out var content) ? content.StringValue : string.Empty,
            Embedding = point.Vectors.Vector.Data.ToArray(),
            IsDeleted = payload.TryGetValue("is_deleted", out var deleted) && deleted.BoolValue,
            CreatedAt = payload.TryGetValue("created_at", out var created) ? DateTime.Parse(created.StringValue) : DateTime.UtcNow,
            UpdatedAt = payload.TryGetValue("updated_at", out var updated) ? DateTime.Parse(updated.StringValue) : null,
            DeletedAt = payload.TryGetValue("deleted_at", out var deletedAt) && !string.IsNullOrEmpty(deletedAt.StringValue) ? DateTime.Parse(deletedAt.StringValue) : null
        };
    }

    private static VectorDocument MapScoredPointToDocument(ScoredPoint point)
    {
        var payload = point.Payload;
        return new VectorDocument
        {
            Id = point.Id.Uuid,
            Content = payload.TryGetValue("content", out var content) ? content.StringValue : string.Empty,
            Embedding = point.Vectors.Vector.Data.ToArray(),
            IsDeleted = payload.TryGetValue("is_deleted", out var deleted) && deleted.BoolValue,
            CreatedAt = payload.TryGetValue("created_at", out var created) ? DateTime.Parse(created.StringValue) : DateTime.UtcNow,
            UpdatedAt = payload.TryGetValue("updated_at", out var updated) ? DateTime.Parse(updated.StringValue) : null,
            DeletedAt = payload.TryGetValue("deleted_at", out var deletedAt) && !string.IsNullOrEmpty(deletedAt.StringValue) ? DateTime.Parse(deletedAt.StringValue) : null
        };
    }
}
