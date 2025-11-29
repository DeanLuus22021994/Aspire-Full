using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Aspire_Full.Gateway.Services;

public interface IVectorStoreService
{
    Task UpsertUserVectorAsync(int userId, string displayName, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken = default);
    Task DeleteUserVectorAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScoredPoint>> SearchAsync(ReadOnlyMemory<float> embedding, int limit = 10, CancellationToken cancellationToken = default);
}

public class VectorStoreService : IVectorStoreService
{
    private readonly QdrantClient _client;
    private readonly ILogger<VectorStoreService> _logger;
    private const string CollectionName = "users";
    private const ulong VectorSize = 384; // Matching all-MiniLM-L6-v2

    public VectorStoreService(QdrantClient client, ILogger<VectorStoreService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken);
        if (!collections.Contains(CollectionName))
        {
            _logger.LogInformation("Creating Qdrant collection {CollectionName}", CollectionName);
            await _client.CreateCollectionAsync(CollectionName, new VectorParams { Size = VectorSize, Distance = Distance.Cosine }, cancellationToken: cancellationToken);
        }
    }

    public async Task UpsertUserVectorAsync(int userId, string displayName, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);

        var point = new PointStruct
        {
            Id = (ulong)userId,
            Vectors = embedding.ToArray(),
            Payload = {
                ["display_name"] = displayName,
                ["updated_at"] = DateTime.UtcNow.ToString("O")
            }
        };

        await _client.UpsertAsync(CollectionName, [point], cancellationToken: cancellationToken);
        _logger.LogInformation("Upserted vector for user {UserId}", userId);
    }

    public async Task DeleteUserVectorAsync(int userId, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);
        await _client.DeleteAsync(CollectionName, [(ulong)userId], cancellationToken: cancellationToken);
        _logger.LogInformation("Deleted vector for user {UserId}", userId);
    }

    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(ReadOnlyMemory<float> embedding, int limit = 10, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);
        return await _client.SearchAsync(CollectionName, embedding.ToArray(), limit: (ulong)limit, cancellationToken: cancellationToken);
    }
}
