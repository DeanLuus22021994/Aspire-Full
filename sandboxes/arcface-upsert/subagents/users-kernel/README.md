# Users Kernel Sub-Agent

## Mission
Provide a sandbox version of the Users controller + persistence layer so we can run ArcFace-driven upsert/downsert experiments without touching production data.

## Responsibilities
- Host EF Core context + migrations scoped to a sandbox database (SQLite/Postgres).
- Implement the same REST surface as `Aspire_Full.Api.UsersController` (upsert, update, downsert, login).
- Invoke the Embedding Service before writing/updating vector payloads.
- Coordinate with the Vector Store agent to keep embeddings synchronized with entity state.

## Out of Scope
- Direct interaction with ArcFace ONNX or Qdrant internals.
- Managing authentication/authorization beyond what tests require.

## Integration Contract
- Consumes `IArcFaceEmbeddingService` and `ISandboxVectorStore` interfaces.
- Emits domain events/logs summarizing user state transitions for test assertions.
- Provides seed data + fixtures under `Sandbox.Users.Tests`.

## Pending Decisions
- Choose sandbox database (default: SQLite for speed; Postgres optional via docker-compose).
- Determine whether to expose the API via Minimal APIs or MVC (default: controllers for parity).
