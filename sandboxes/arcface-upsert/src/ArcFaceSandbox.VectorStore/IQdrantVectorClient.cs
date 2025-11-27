using System.Collections.Generic;
using Qdrant.Client.Grpc;

namespace ArcFaceSandbox.VectorStore;

public interface IQdrantVectorClient : IAsyncDisposable
{
    Task<IReadOnlyCollection<string>> ListCollectionsAsync(CancellationToken cancellationToken);

    Task CreateCollectionAsync(string name, VectorParams vectorParams, CancellationToken cancellationToken);

    Task UpsertAsync(string collectionName, IReadOnlyList<PointStruct> points, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        string collectionName,
        ReadOnlyMemory<float> vector,
        Filter? filter,
        ulong limit,
        bool withPayload,
        bool withVectors,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(
        string collectionName,
        IReadOnlyList<PointId> ids,
        bool withPayload,
        bool withVectors,
        CancellationToken cancellationToken);
}
