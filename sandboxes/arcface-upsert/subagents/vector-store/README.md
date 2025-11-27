# Vector Store Sub-Agent

## Mission
Expose Qdrant-backed persistence tailored to ArcFace embeddings while mirroring the production upsert/downsert semantics.

## Responsibilities
- Ensure the `arcface-sandbox` collection exists with size=512 and cosine distance.
- Validate all embeddings before upsert; reject mismatched vectors with actionable errors.
- Provide CRUD + search APIs compatible with `IVectorStoreService` from production code.
- Record soft-delete metadata (`is_deleted`, `deleted_at`) just like [Aspire-Full.VectorStore](../../../Aspire-Full.VectorStore/VectorStoreService.cs).

## Out of Scope
- Generating embeddings
- Managing HTTP transport
- Owning business logic for users

## Interfaces (Draft)
```csharp
public interface ISandboxVectorStore
{
    Task<VectorDocument> UpsertAsync(VectorDocument doc, CancellationToken ct = default);
    Task<bool> DownsertAsync(string id, CancellationToken ct = default);
    Task<IList<VectorDocument>> SearchAsync(ReadOnlyMemory<float> query, int topK = 10, CancellationToken ct = default);
}
```

## Operational Notes
- Qdrant endpoint defaults to `http://localhost:6334`; override via `SANDBOX_QDRANT_ENDPOINT`.
- Collection names include a timestamp suffix when running tests to avoid collisions.
