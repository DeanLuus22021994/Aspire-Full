using System.Collections.Generic;
using System.Linq;
using ArcFaceSandbox.VectorStore;
using Google.Protobuf.Collections;
using Qdrant.Client.Grpc;

namespace ArcFaceSandbox.VectorStore.Tests;

internal sealed class FakeQdrantClient : IQdrantVectorClient
{
    private readonly Dictionary<string, PointStruct> _store = new(StringComparer.Ordinal);

    public HashSet<string> Collections { get; } = new(StringComparer.Ordinal)
    {
        SandboxVectorStoreOptions.DefaultCollectionName
    };

    public Task<IReadOnlyCollection<string>> ListCollectionsAsync(CancellationToken cancellationToken)
        => Task.FromResult((IReadOnlyCollection<string>)Collections.ToArray());

    public Task CreateCollectionAsync(string name, VectorParams vectorParams, CancellationToken cancellationToken)
    {
        Collections.Add(name);
        return Task.CompletedTask;
    }

    public Task UpsertAsync(string collectionName, IReadOnlyList<PointStruct> points, CancellationToken cancellationToken)
    {
        foreach (var point in points)
        {
            var clone = point.Clone();
            _store[clone.Id.Uuid] = clone;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        string collectionName,
        ReadOnlyMemory<float> vector,
        Filter? filter,
        ulong limit,
        bool withPayload,
        bool withVectors,
        CancellationToken cancellationToken)
    {
        var includeDeleted = filter is null;
        var items = _store.Values
            .Where(p => includeDeleted || !(p.Payload.TryGetValue("is_deleted", out var deleted) && deleted.BoolValue))
            .Take((int)limit)
            .Select(ToScoredPoint)
            .Cast<ScoredPoint>()
            .ToList();

        return Task.FromResult((IReadOnlyList<ScoredPoint>)items);
    }

    public Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(
        string collectionName,
        IReadOnlyList<PointId> ids,
        bool withPayload,
        bool withVectors,
        CancellationToken cancellationToken)
    {
        var results = new List<RetrievedPoint>();
        foreach (var id in ids)
        {
            if (_store.TryGetValue(id.Uuid, out var point))
            {
                results.Add(ToRetrievedPoint(point));
            }
        }

        return Task.FromResult((IReadOnlyList<RetrievedPoint>)results);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ScoredPoint ToScoredPoint(PointStruct point)
    {
        var scored = new ScoredPoint
        {
            Id = new PointId { Uuid = point.Id.Uuid },
            Score = 0,
            Vectors = CloneVectors(point)
        };

        CopyPayload(point.Payload, scored.Payload);
        return scored;
    }

    private static RetrievedPoint ToRetrievedPoint(PointStruct point)
    {
        var retrieved = new RetrievedPoint
        {
            Id = new PointId { Uuid = point.Id.Uuid },
            Vectors = CloneVectors(point)
        };

        CopyPayload(point.Payload, retrieved.Payload);
        return retrieved;
    }

    private static void CopyPayload(MapField<string, Value> source, MapField<string, Value> target)
    {
        foreach (var (key, value) in source)
        {
            target[key] = value;
        }
    }

    private static VectorsOutput CloneVectors(PointStruct point)
    {
        var vectors = point.Vectors;
        if (vectors is null)
        {
            return new VectorsOutput();
        }

        if (vectors.Vector is { } single)
        {
            return new VectorsOutput { Vector = CloneVector(single) };
        }

        if (vectors.Vectors_ is { } named && named.Vectors.Count > 0)
        {
            var namedOutput = new NamedVectorsOutput();
            foreach (var entry in named.Vectors)
            {
                namedOutput.Vectors[entry.Key] = CloneVector(entry.Value);
            }

            return new VectorsOutput { Vectors = namedOutput };
        }

        return new VectorsOutput();
    }

    private static VectorOutput CloneVector(Vector source)
    {
        var output = new VectorOutput();

        if (source.Dense is not null)
        {
            var dense = new DenseVector();
            dense.Data.Add(source.Dense.Data);
            output.Dense = dense;
        }

        if (source.MultiDense is not null)
        {
            var multi = new MultiDenseVector();
            foreach (var denseVector in source.MultiDense.Vectors)
            {
                var copy = new DenseVector();
                copy.Data.Add(denseVector.Data);
                multi.Vectors.Add(copy);
            }

            output.MultiDense = multi;
        }

        if (source.Sparse is not null)
        {
            var sparse = new SparseVector();
            sparse.Values.Add(source.Sparse.Values);
            sparse.Indices.Add(source.Sparse.Indices);
            output.Sparse = sparse;
        }

        return output;
    }
}
