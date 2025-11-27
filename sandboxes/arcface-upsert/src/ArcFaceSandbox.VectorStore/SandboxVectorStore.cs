using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;

namespace ArcFaceSandbox.VectorStore;

/// <summary>
/// Sandbox scoped vector store with enforced 512-dimension embeddings.
/// </summary>
public sealed class SandboxVectorStore : ISandboxVectorStore
{
    private readonly IQdrantVectorClient _client;
    private readonly SandboxVectorStoreOptions _options;
    private readonly ILogger<SandboxVectorStore> _logger;
    private readonly SemaphoreSlim _collectionGate = new(1, 1);
    private bool _collectionReady;

    public SandboxVectorStore(
        IQdrantVectorClient client,
        IOptions<SandboxVectorStoreOptions> options,
        ILogger<SandboxVectorStore> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
        => EnsureCollectionInternalAsync(cancellationToken);

    public async Task<SandboxVectorDocument> UpsertAsync(SandboxVectorDocument document, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(document);
        ThrowHelper.ValidateVectorLength(document.Embedding.Length, _options.VectorSize);
        _ = ThrowHelper.ValidateGuid(document.Id);

        await EnsureCollectionInternalAsync(cancellationToken).ConfigureAwait(false);

        var existing = await GetAsync(document.Id, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var payload = BuildPayload(document.Content, document.Metadata, isDeleted: false, existing?.CreatedAt ?? now, now);

        var point = new PointStruct
        {
            Id = new PointId { Uuid = document.Id },
            Vectors = document.Embedding.ToArray(),
            Payload = { payload }
        };

        await _client.UpsertAsync(_options.CollectionName, new[] { point }, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Upserted sandbox vector {Id}", document.Id);

        return document with
        {
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    public async Task<bool> DownsertAsync(string id, CancellationToken cancellationToken = default)
    {
        _ = ThrowHelper.ValidateGuid(id);
        await EnsureCollectionInternalAsync(cancellationToken).ConfigureAwait(false);

        var existing = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var payload = BuildPayload(existing.Content, existing.Metadata, isDeleted: true, existing.CreatedAt, now, now);

        var point = new PointStruct
        {
            Id = new PointId { Uuid = existing.Id },
            Vectors = existing.Embedding.ToArray(),
            Payload = { payload }
        };

        await _client.UpsertAsync(_options.CollectionName, new[] { point }, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Soft deleted sandbox vector {Id}", id);
        return true;
    }

    public async Task<IList<SandboxVectorDocument>> SearchAsync(ReadOnlyMemory<float> embedding, int topK = 10, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ValidateVectorLength(embedding.Length, _options.VectorSize);
        ThrowHelper.ValidateTopK(topK);
        await EnsureCollectionInternalAsync(cancellationToken).ConfigureAwait(false);

        Filter? filter = includeDeleted ? null : new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "is_deleted",
                        Match = new Match { Boolean = false }
                    }
                }
            }
        };

        var results = await _client.SearchAsync(
            _options.CollectionName,
            embedding,
            filter,
            (ulong)topK,
            withPayload: true,
            withVectors: true,
            cancellationToken).ConfigureAwait(false);

        return results.Select(MapScoredPoint).ToList();
    }

    public async Task<SandboxVectorDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        _ = ThrowHelper.ValidateGuid(id);
        await EnsureCollectionInternalAsync(cancellationToken).ConfigureAwait(false);

        var points = await _client.RetrieveAsync(
            _options.CollectionName,
            new[] { new PointId { Uuid = id } },
            withPayload: true,
            withVectors: true,
            cancellationToken).ConfigureAwait(false);

        var point = points.FirstOrDefault();
        return point is null ? null : MapRetrievedPoint(point);
    }

    private async Task EnsureCollectionInternalAsync(CancellationToken cancellationToken)
    {
        if (_collectionReady)
        {
            return;
        }

        await _collectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_collectionReady)
            {
                return;
            }

            var collections = await _client.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);
            if (!collections.Contains(_options.CollectionName) && _options.AutoCreateCollection)
            {
                var vectorParams = new VectorParams
                {
                    Size = (ulong)_options.VectorSize,
                    Distance = Distance.Cosine
                };

                await _client.CreateCollectionAsync(_options.CollectionName, vectorParams, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Created sandbox collection {Collection}", _options.CollectionName);
            }

            _collectionReady = true;
        }
        finally
        {
            _collectionGate.Release();
        }
    }

    private static Dictionary<string, Value> BuildPayload(
        string content,
        IReadOnlyDictionary<string, string>? metadata,
        bool isDeleted,
        DateTime createdAt,
        DateTime updatedAt,
        DateTime? deletedAt = null)
    {
        var payload = new Dictionary<string, Value>
        {
            ["content"] = content,
            ["is_deleted"] = isDeleted,
            ["created_at"] = createdAt.ToString("O"),
            ["updated_at"] = updatedAt.ToString("O")
        };

        if (deletedAt is not null)
        {
            payload["deleted_at"] = deletedAt.Value.ToString("O");
        }

        if (metadata is not null)
        {
            foreach (var (key, value) in metadata)
            {
                payload[$"meta_{key}"] = value ?? string.Empty;
            }
        }

        return payload;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SandboxVectorDocument MapRetrievedPoint(RetrievedPoint point)
        => MapDocument(point.Id.Uuid, point.Payload, point.Vectors.Vector.Data);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SandboxVectorDocument MapScoredPoint(ScoredPoint point)
        => MapDocument(point.Id.Uuid, point.Payload, point.Vectors.Vector.Data);

    private static SandboxVectorDocument MapDocument(string id, IDictionary<string, Value> payload, IReadOnlyList<float> vector)
    {
        return new SandboxVectorDocument
        {
            Id = id,
            Content = payload.TryGetValue("content", out var content) ? content.StringValue : string.Empty,
            Embedding = vector.ToArray(),
            IsDeleted = payload.TryGetValue("is_deleted", out var deleted) && deleted.BoolValue,
            CreatedAt = payload.TryGetValue("created_at", out var created) ? DateTime.Parse(created.StringValue) : DateTime.UtcNow,
            UpdatedAt = payload.TryGetValue("updated_at", out var updated) ? DateTime.Parse(updated.StringValue) : null,
            DeletedAt = payload.TryGetValue("deleted_at", out var deletedAt) && !string.IsNullOrEmpty(deletedAt.StringValue)
                ? DateTime.Parse(deletedAt.StringValue)
                : null
        };
    }
}
