using System.Collections.Generic;
using ArcFaceSandbox.VectorStore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ArcFaceSandbox.VectorStore.Tests;

public sealed class SandboxVectorStoreTests
{
    private static SandboxVectorStore CreateStore(FakeQdrantClient client)
    {
        var options = Options.Create(new SandboxVectorStoreOptions());
        var logger = NullLogger<SandboxVectorStore>.Instance;
        return new SandboxVectorStore(client, options, logger);
    }

    [Fact]
    public async Task UpsertAsync_PersistsDocument()
    {
        var client = new FakeQdrantClient();
        var store = CreateStore(client);
        var doc = CreateDocument();

        var result = await store.UpsertAsync(doc);

        Assert.False(result.IsDeleted);
        Assert.Contains(SandboxVectorStoreOptions.DefaultCollectionName, client.Collections);
        var stored = await store.GetAsync(doc.Id);
        Assert.NotNull(stored);
        Assert.Equal(doc.Content, stored!.Content);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task UpsertAsync_InvalidVector_Throws()
    {
        var client = new FakeQdrantClient();
        var store = CreateStore(client);
        var doc = CreateDocument(16);

        await Assert.ThrowsAsync<ArgumentException>(() => store.UpsertAsync(doc));
    }

    [Fact]
    public async Task DownsertAsync_SetsDeletedFlags()
    {
        var client = new FakeQdrantClient();
        var store = CreateStore(client);
        var doc = CreateDocument();
        await store.UpsertAsync(doc);

        var deleted = await store.DownsertAsync(doc.Id);

        Assert.True(deleted);
        var stored = await store.GetAsync(doc.Id);
        Assert.True(stored!.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
    }

    [Fact]
    public async Task SearchAsync_RespectsDeletedFilter()
    {
        var client = new FakeQdrantClient();
        var store = CreateStore(client);
        var active = CreateDocument();
        var stale = CreateDocument();
        await store.UpsertAsync(active);
        await store.UpsertAsync(stale);
        await store.DownsertAsync(stale.Id);

        var results = await store.SearchAsync(active.Embedding);

        Assert.Single(results);
        Assert.Equal(active.Id, results[0].Id);

        var withDeleted = await store.SearchAsync(active.Embedding, includeDeleted: true);
        Assert.Equal(2, withDeleted.Count);
    }

    private static SandboxVectorDocument CreateDocument(int vectorSize = SandboxVectorStoreOptions.DefaultVectorSize)
    {
        return new SandboxVectorDocument
        {
            Id = Guid.NewGuid().ToString(),
            Content = "tester",
            Embedding = new float[vectorSize],
            Metadata = new Dictionary<string, string> { ["role"] = "admin" }
        };
    }
}
