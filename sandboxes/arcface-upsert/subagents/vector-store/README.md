# Vector Store Sub-Agent

## Mission
Expose Qdrant-backed persistence tailored to ArcFace embeddings while mirroring the production upsert/downsert semantics, with status surfaced through the shared sandbox UI rather than Aspire wiring for now.

## Responsibilities
- Ensure the `arcface-sandbox` collection exists with size=512 and cosine distance.
- Validate all embeddings before upsert; reject mismatched vectors with actionable errors.
- Provide CRUD + search APIs compatible with `IVectorStoreService` from production code (other solutions can defer direct integration until later).
- Record soft-delete metadata (`is_deleted`, `deleted_at`) just like [Aspire-Full.VectorStore](../../../Aspire-Full.VectorStore/VectorStoreService.cs).

## Out of Scope
- Generating embeddings
- Managing HTTP transport
- Owning business logic for users

## Interfaces
```csharp
public interface ISandboxVectorStore
{
    Task<SandboxVectorDocument> UpsertAsync(SandboxVectorDocument doc, CancellationToken ct = default);
    Task<bool> DownsertAsync(string id, CancellationToken ct = default);
    Task<IList<SandboxVectorDocument>> SearchAsync(ReadOnlyMemory<float> query, int topK = 10, bool includeDeleted = false, CancellationToken ct = default);
    Task<SandboxVectorDocument?> GetAsync(string id, CancellationToken ct = default);
    Task EnsureCollectionAsync(CancellationToken ct = default);
}
```

### DI Registration
```csharp
builder.Services.AddSandboxVectorStore(builder.Configuration);
```

Configuration keys (section `ArcFace:VectorStore`):

| Key | Description | Default |
| --- | --- | --- |
| `Endpoint` | Qdrant endpoint (HTTP or gRPC). | `http://localhost:6334` |
| `CollectionName` | Sandbox collection name. | `arcface-sandbox` |
| `VectorSize` | Must remain 512 for ArcFace. | `512` |
| `ApiKey` | Optional token for managed clusters. | _null_ |
| `AutoCreateCollection` | Ensure collection exists on startup. | `true` |

## Operational Notes
- Qdrant endpoint defaults to `http://localhost:6334`; override via config/env.
- Test runs can override `CollectionName` to append build-specific suffixes.

## UI Requirement (Page 3 of 3)
- Deliver a "Vector Store Monitor" page within the sandbox semantic UI that:
    - Shows collection health (name, vector size, distance metric, doc count, auto-create status).
    - Streams recent upsert/downsert operations with document IDs and delete flags.
    - Provides a lightweight similarity search panel for manual queries against stored embeddings.
- The page should read only from the Vector Store agent endpoints/fakes and does not require Aspire AppHost registration yet.
