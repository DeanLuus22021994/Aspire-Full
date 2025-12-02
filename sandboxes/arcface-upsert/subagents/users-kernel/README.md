# Users Kernel Sub-Agent

## Mission
Provide a sandbox version of the Users controller + persistence layer so we can run ArcFace-driven experiments (upsert/downsert or future flows) without touching production data, while remaining a standalone API/UI surface.

## Responsibilities
- Host EF Core context + migrations scoped to a sandbox database (SQLite/Postgres).
- Implement the same REST surface as `Aspire_Full.Api.UsersController` (upsert, update, downsert, login) but allow downstream solutions to defer calling these endpoints until integration time.
- Invoke the Embedding Service before writing/updating vector payloads.
- Coordinate with the Vector Store agent to keep embeddings synchronized with entity state.
- Surface state via the sandbox UI described below so stakeholders can test flows without wiring the controller into Aspire AppHost yet.

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

## UI Requirement (Page 2 of 3)
- Build a "Users Sandbox" page that consumes the Users Kernel API to:
	- List active/inactive sandbox users with search + filter affordances.
	- Trigger upsert/update/downsert/login flows via buttons that call the existing endpoints.
	- Display embedding/vector sync status per user (green when vector store + DB agree).
- The page should stand alone (no AppHost dependency) and align with the shared semantic UI shell used by the Embedding and Vector Store agents.

## Integration Status
- Users Kernel remains an independent API; Aspire AppHost wiring is intentionally deferred until the teams agree on the cross-solution contract.
- Continue validating functionality through the integration tests under `tests/ArcFaceSandbox.UsersKernel.Tests`.

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
