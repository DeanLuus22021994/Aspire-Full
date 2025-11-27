# Users Kernel Sub-Agent

## Mission
Provide a sandbox version of the Users controller + persistence layer so we can run ArcFace-driven upsert/downsert experiments without touching production data.

## Responsibilities
- Host EF Core context + migrations scoped to a sandbox database (SQLite/Postgres).
- Implement the same REST surface as `Aspire_Full.Api.UsersController` (upsert, update, downsert, login).
- Invoke the Embedding Service before writing/updating vector payloads.
- Coordinate with the Vector Store agent to keep embeddings synchronized with entity state.

### Current Surface
| Method | Route | Notes |
| --- | --- | --- |
| `GET /api/users` | Returns all active sandbox users ordered by `CreatedAt`. |
| `GET /api/users/{id}` | Fetch single active user by identifier. |
| `GET /api/users/by-email/{email}` | Fetch user by email (active only). |
| `POST /api/users` | Upsert (create/reactivate) a user. Requires `faceImageBase64` payload that is passed to the Embedding Service before persisting and updating Qdrant. |
| `PUT /api/users/{id}` | Update display name/role/active flag optionally including a new aligned face image. Metadata-only updates reuse the latest vector from Qdrant. |
| `DELETE /api/users/{id}` | Soft deletes (downserts) the user and marks the Qdrant document as deleted. |
| `POST /api/users/{id}/login` | Records a `LastLoginAt` timestamp for telemetry parity. |

Responses follow `SandboxUserResponse`, which mirrors the production `UserResponseDto` fields plus soft-delete metadata.

## Out of Scope
- Direct interaction with ArcFace ONNX or Qdrant internals.
- Managing authentication/authorization beyond what tests require.

## Integration Contract
- Consumes `IArcFaceEmbeddingService` and `ISandboxVectorStore` interfaces.
- Emits domain events/logs summarizing user state transitions for test assertions.
- Provides seed data + fixtures under `Sandbox.Users.Tests`.

### Configuration
```jsonc
"ConnectionStrings": {
	"Users": "Data Source=arcface-sandbox-users.db"
},
"ArcFace": {
	"Embedding": {
		"ModelPath": "./models/arcface_r100_v1.onnx"
	},
	"VectorStore": {
		"Endpoint": "http://localhost:6334",
		"CollectionName": "arcface-sandbox",
		"VectorSize": 512,
		"AutoCreateCollection": true
	}
}
```

Run `dotnet run --project src/ArcFaceSandbox.UsersKernel.Api` after supplying a valid ArcFace ONNX model and a reachable Qdrant instance.

## Pending Decisions
- Choose sandbox database (default: SQLite for speed; Postgres optional via docker-compose).
- Determine whether to expose the API via Minimal APIs or MVC (default: controllers for parity).
