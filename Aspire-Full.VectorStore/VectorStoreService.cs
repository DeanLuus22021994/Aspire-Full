using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
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
    /// Upsert multiple documents in batch for GPU-efficient bulk operations.
    /// </summary>
    IAsyncEnumerable<VectorDocument> UpsertBatchAsync(IEnumerable<VectorDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downsert (soft-delete) a document - marks as deleted but retains data.
    /// </summary>
    Task<bool> DownsertAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard delete a document - permanently removes from store.
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard delete multiple documents in batch.
    /// </summary>
    Task<int> DeleteBatchAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar documents using vector similarity.
    /// </summary>
    Task<IList<VectorDocument>> SearchAsync(ReadOnlyMemory<float> queryVector, int topK = 10, bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search multiple vectors in batch for GPU-efficient bulk similarity search.
    /// </summary>
    IAsyncEnumerable<IList<VectorDocument>> SearchBatchAsync(IEnumerable<ReadOnlyMemory<float>> queryVectors, int topK = 10, bool includeDeleted = false, CancellationToken cancellationToken = default);

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
/// Optimized for GPU-accelerated workloads with SIMD support.
/// </summary>
public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorStoreService> _logger;
    private readonly string _collectionName;
    private readonly int _vectorSize;
    private static readonly ArrayPool<float> VectorPool = ArrayPool<float>.Shared;

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

        LogHardwareCapabilities();
    }

    private void LogHardwareCapabilities()
    {
        _logger.LogInformation(
            "VectorStore initialized - SIMD: {SimdEnabled}, Vector<float>.Count: {VectorCount}, AVX2: {Avx2}, AVX512: {Avx512}",
            System.Numerics.Vector.IsHardwareAccelerated,
            System.Numerics.Vector<float>.Count,
            Avx2.IsSupported,
            Avx512F.IsSupported);
    }

    public async Task EnsureCollectionAsync(string collectionName, int vectorSize, CancellationToken cancellationToken = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);
        if (!collections.Contains(collectionName))
        {
            await _client.CreateCollectionAsync(
                collectionName,
                new VectorParams { Size = (ulong)vectorSize, Distance = Distance.Cosine },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Created Qdrant collection: {CollectionName}", collectionName);
        }
    }

    public async Task<VectorDocument> UpsertAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(document);
        ThrowHelper.ValidateVectorDimension(document.Embedding.Length, _vectorSize);
        var guid = ThrowHelper.ValidateAndParseGuid(document.Id);

        var existingDoc = await GetAsync(document.Id, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;

        var payload = new Dictionary<string, Value>
        {
            ["content"] = document.Content,
            ["is_deleted"] = false,
            ["created_at"] = existingDoc?.CreatedAt.ToString("O") ?? now.ToString("O"),
            ["updated_at"] = now.ToString("O")
        };

        if (document.Metadata is { } metadata)
        {
            foreach (var kvp in metadata)
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

        await _client.UpsertAsync(_collectionName, [point], cancellationToken: cancellationToken).ConfigureAwait(false);

        var action = existingDoc is null ? "Created" : (existingDoc.IsDeleted ? "Reactivated" : "Updated");
        _logger.LogInformation("{Action} document {Id} in collection {Collection}", action, document.Id, _collectionName);

        return document with
        {
            CreatedAt = existingDoc?.CreatedAt ?? now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    public async IAsyncEnumerable<VectorDocument> UpsertBatchAsync(
        IEnumerable<VectorDocument> documents,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(documents);

        var batch = new List<PointStruct>();
        var docList = new List<VectorDocument>();
        var now = DateTime.UtcNow;

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ThrowHelper.ThrowIfNull(document);
            ThrowHelper.ValidateVectorDimension(document.Embedding.Length, _vectorSize);
            _ = ThrowHelper.ValidateAndParseGuid(document.Id);

            var payload = new Dictionary<string, Value>
            {
                ["content"] = document.Content,
                ["is_deleted"] = false,
                ["created_at"] = now.ToString("O"),
                ["updated_at"] = now.ToString("O")
            };

            if (document.Metadata is { } metadata)
            {
                foreach (var kvp in metadata)
                {
                    payload[$"meta_{kvp.Key}"] = kvp.Value?.ToString() ?? string.Empty;
                }
            }

            batch.Add(new PointStruct
            {
                Id = new PointId { Uuid = document.Id },
                Vectors = document.Embedding.ToArray(),
                Payload = { payload }
            });

            docList.Add(document with { CreatedAt = now, UpdatedAt = now, IsDeleted = false, DeletedAt = null });

            // Batch upsert every 100 documents for GPU efficiency
            if (batch.Count >= 100)
            {
                await _client.UpsertAsync(_collectionName, batch, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Batch upserted {Count} documents to {Collection}", batch.Count, _collectionName);

                foreach (var doc in docList)
                {
                    yield return doc;
                }

                batch.Clear();
                docList.Clear();
            }
        }

        // Upsert remaining documents
        if (batch.Count > 0)
        {
            await _client.UpsertAsync(_collectionName, batch, cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Batch upserted {Count} documents to {Collection}", batch.Count, _collectionName);

            foreach (var doc in docList)
            {
                yield return doc;
            }
        }
    }

    public async Task<bool> DownsertAsync(string id, CancellationToken cancellationToken = default)
    {
        _ = ThrowHelper.ValidateAndParseGuid(id);

        var existing = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null) return false;

        var now = DateTime.UtcNow;
        var softDeleted = existing with
        {
            IsDeleted = true,
            DeletedAt = now,
            UpdatedAt = now
        };

        await UpsertSoftDeleteAsync(softDeleted, cancellationToken).ConfigureAwait(false);
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

        await _client.UpsertAsync(_collectionName, [point], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var guid = ThrowHelper.ValidateAndParseGuid(id);

        await _client.DeleteAsync(_collectionName, guid, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Hard-deleted document {Id} from collection {Collection}", id, _collectionName);
        return true;
    }

    public async Task<int> DeleteBatchAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(ids);

        var guidList = new List<Guid>();
        foreach (var id in ids)
        {
            guidList.Add(ThrowHelper.ValidateAndParseGuid(id));
        }

        if (guidList.Count == 0) return 0;

        await _client.DeleteAsync(_collectionName, guidList, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Batch hard-deleted {Count} documents from {Collection}", guidList.Count, _collectionName);
        return guidList.Count;
    }

    public async Task<VectorDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        _ = ThrowHelper.ValidateAndParseGuid(id);

        try
        {
            var pointIds = new List<PointId> { new PointId { Uuid = id } };
            var points = await _client.RetrieveAsync(
                _collectionName,
                pointIds,
                withPayload: true,
                withVectors: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var point = points.FirstOrDefault();
            return point is { } p ? MapPointToDocument(p) : null;
        }
        catch
        {
            return null;
        }
    }

    [SkipLocalsInit]
    public async Task<IList<VectorDocument>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topK = 10,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        ThrowHelper.ValidateVectorDimension(queryVector.Length, _vectorSize);
        ThrowHelper.ValidateTopK(topK);

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
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return results.Select(MapScoredPointToDocument).ToList();
    }

    public async IAsyncEnumerable<IList<VectorDocument>> SearchBatchAsync(
        IEnumerable<ReadOnlyMemory<float>> queryVectors,
        int topK = 10,
        bool includeDeleted = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(queryVectors);
        ThrowHelper.ValidateTopK(topK);

        foreach (var queryVector in queryVectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await SearchAsync(queryVector, topK, includeDeleted, cancellationToken).ConfigureAwait(false);
        }
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
