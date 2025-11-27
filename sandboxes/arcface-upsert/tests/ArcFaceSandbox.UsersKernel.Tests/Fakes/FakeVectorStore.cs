using ArcFaceSandbox.VectorStore;

namespace ArcFaceSandbox.UsersKernel.Tests.Fakes;

internal sealed class FakeVectorStore : ISandboxVectorStore
{
    private readonly Dictionary<string, SandboxVectorDocument> _documents = new(StringComparer.Ordinal);

    public SandboxVectorDocument? LastUpsert { get; private set; }

    public IReadOnlyDictionary<string, SandboxVectorDocument> Documents => _documents;

    public void Reset() => _documents.Clear();

    public Task EnsureCollectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<SandboxVectorDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        _documents.TryGetValue(id, out var doc);
        return Task.FromResult<SandboxVectorDocument?>(doc);
    }

    public Task<bool> DownsertAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_documents.TryGetValue(id, out var doc))
        {
            var snapshot = doc with { IsDeleted = true, DeletedAt = DateTime.UtcNow };
            _documents[id] = snapshot;
            LastUpsert = snapshot;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<IList<SandboxVectorDocument>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        int topK = 10,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var documents = includeDeleted
            ? _documents.Values.ToList()
            : _documents.Values.Where(d => !d.IsDeleted).ToList();

        return Task.FromResult<IList<SandboxVectorDocument>>(documents);
    }

    public Task<SandboxVectorDocument> UpsertAsync(SandboxVectorDocument document, CancellationToken cancellationToken = default)
    {
        var snapshot = document with
        {
            Embedding = document.Embedding.ToArray()
        };

        _documents[snapshot.Id] = snapshot;
        LastUpsert = snapshot;
        return Task.FromResult(snapshot);
    }
}
