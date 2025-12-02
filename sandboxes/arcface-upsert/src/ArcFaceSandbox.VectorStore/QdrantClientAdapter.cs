using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ArcFaceSandbox.VectorStore;

internal sealed class QdrantClientAdapter : IQdrantVectorClient
{
    private readonly QdrantClient _client;

    public QdrantClientAdapter(IOptions<SandboxVectorStoreOptions> options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        var opts = options.Value;
        if (!Uri.TryCreate(opts.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException($"Invalid Qdrant endpoint '{opts.Endpoint}'.");
        }

        _client = new QdrantClient(endpoint, opts.ApiKey, loggerFactory: loggerFactory);
    }

    public async Task<IReadOnlyCollection<string>> ListCollectionsAsync(CancellationToken cancellationToken)
    {
        var response = await _client.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);
        return response.ToArray();
    }

    public Task CreateCollectionAsync(string name, VectorParams vectorParams, CancellationToken cancellationToken)
        => _client.CreateCollectionAsync(name, vectorParams, cancellationToken: cancellationToken);

    public Task UpsertAsync(string collectionName, IReadOnlyList<PointStruct> points, CancellationToken cancellationToken)
        => _client.UpsertAsync(collectionName, points, cancellationToken: cancellationToken);

    public Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        string collectionName,
        ReadOnlyMemory<float> vector,
        Filter? filter,
        ulong limit,
        bool withPayload,
        bool withVectors,
        CancellationToken cancellationToken)
        => _client.SearchAsync(collectionName, vector, filter: filter, limit: limit, payloadSelector: withPayload, vectorsSelector: withVectors, cancellationToken: cancellationToken);

    public Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(
        string collectionName,
        IReadOnlyList<PointId> ids,
        bool withPayload,
        bool withVectors,
        CancellationToken cancellationToken)
        => _client.RetrieveAsync(collectionName, ids, withPayload, withVectors, cancellationToken: cancellationToken);

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
