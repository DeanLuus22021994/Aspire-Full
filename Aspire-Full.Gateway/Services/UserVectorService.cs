using Aspire_Full.VectorStore;

namespace Aspire_Full.Gateway.Services;

public interface IUserVectorService
{
    Task UpsertUserVectorAsync(int userId, string displayName, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken = default);
    Task DeleteUserVectorAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorDocument>> SearchAsync(ReadOnlyMemory<float> embedding, int limit = 10, CancellationToken cancellationToken = default);
}

public class UserVectorService : IUserVectorService
{
    private readonly Aspire_Full.VectorStore.IVectorStoreService _vectorStore;
    private readonly ILogger<UserVectorService> _logger;

    public UserVectorService(Aspire_Full.VectorStore.IVectorStoreService vectorStore, ILogger<UserVectorService> logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
    }

    private string GetDocumentId(int userId)
    {
        // Create a deterministic GUID from the integer ID
        // Using a simple padding strategy for this example
        return new Guid(userId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0).ToString();
    }

    public async Task UpsertUserVectorAsync(int userId, string displayName, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken = default)
    {
        var docId = GetDocumentId(userId);
        var document = new VectorDocument
        {
            Id = docId,
            Content = displayName, // Using Content for display name as primary text
            Embedding = embedding,
            Metadata = new Dictionary<string, object>
            {
                ["user_id"] = userId,
                ["display_name"] = displayName
            }
        };

        // Ensure collection exists - shared service handles this usually via config or we call EnsureCollectionAsync
        // For now, we'll just call UpsertAsync and assume the collection "users" (default) exists or I'll configure it.

        await _vectorStore.UpsertAsync(document, cancellationToken);
        _logger.LogInformation("Upserted vector for user {UserId}", userId);
    }

    public async Task DeleteUserVectorAsync(int userId, CancellationToken cancellationToken = default)
    {
        var docId = GetDocumentId(userId);
        await _vectorStore.DeleteAsync(docId, cancellationToken);
        _logger.LogInformation("Deleted vector for user {UserId}", userId);
    }

    public async Task<IReadOnlyList<VectorDocument>> SearchAsync(ReadOnlyMemory<float> embedding, int limit = 10, CancellationToken cancellationToken = default)
    {
        var results = await _vectorStore.SearchAsync(embedding, limit, cancellationToken: cancellationToken);
        return results.ToList();
    }
}
