using System.Collections.Generic;

namespace ArcFaceSandbox.VectorStore;

/// <summary>
/// Qdrant-backed operations tailored to the ArcFace sandbox.
/// </summary>
public interface ISandboxVectorStore
{
    Task<SandboxVectorDocument> UpsertAsync(SandboxVectorDocument document, CancellationToken cancellationToken = default);

    Task<bool> DownsertAsync(string id, CancellationToken cancellationToken = default);

    Task<IList<SandboxVectorDocument>> SearchAsync(ReadOnlyMemory<float> embedding, int topK = 10, bool includeDeleted = false, CancellationToken cancellationToken = default);

    Task<SandboxVectorDocument?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task EnsureCollectionAsync(CancellationToken cancellationToken = default);
}
