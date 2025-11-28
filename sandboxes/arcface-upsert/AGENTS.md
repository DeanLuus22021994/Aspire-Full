# ArcFace Upsert Sandbox · Agents Catalog

This document repurposes the existing sub-agent specs into a concise reference so each sandbox contributor (or autonomous agent) can identify their scope, dependencies, and deliverables without hunting through multiple files.

## Snapshot
| Agent | Directory | Primary Surface | Key Inputs | Key Outputs | UI Page |
| --- | --- | --- | --- | --- | --- |
| Embedding Service | `subagents/embedding-service/` | ArcFace ONNX runtime harness exposed via `IArcFaceEmbeddingService` | Aligned 112x112 face crops, CUDA execution config, model metadata | Normalized 512-float vectors, model telemetry, hash validation events | Embedding Diagnostics (Page 1/3) |
| Vector Store | `subagents/vector-store/` | Qdrant orchestration via `ISandboxVectorStore` | Embeddings + payload metadata from Users Kernel & Embedding Service | Upsert/downsert confirmations, search results, collection health | Vector Store Monitor (Page 3/3) |
| Users Kernel | `subagents/users-kernel/` | REST API + EF Core persistence mirroring production Users controller | Sandbox HTTP requests, embeddings fetched from Embedding Service | JSON user responses, persistence changes, vector sync events | Users Sandbox (Page 2/3) |

## Embedding Service Agent
- **Mission**: Own ArcFace model lifecycle and expose async embedding generation (single & batch) without letting other components touch ONNX runtime internals.
- **Inputs**: Face image streams, CUDA execution parameters (device visibility, Tensor Core headroom), model path & checksum expectations from `ArcFace:Embedding` config.
- **Outputs**: Normalized `ReadOnlyMemory<float>` vectors, model info (name/version/provider/hash), latency and failure telemetry surfaced via diagnostics endpoints/metrics.
- **Interfaces**: `IArcFaceEmbeddingService` plus DI helper `AddArcFaceEmbedding` (see `subagents/embedding-service/README.md`). Consumers pull embeddings before persisting users or searching vectors.
- **UI Expectations**: Power the “Embedding Diagnostics” page by exposing REST diagnostics: runtime metadata, sample embedding endpoint, telemetry summaries, and placeholder sparkline data when the backend is offline.
- **Constraints & Notes**:
  - Rejects unsigned/invalid models (hash validation before first inference).
  - Handles both single and batch inference; batching obeys `TensorCoreHeadroom` for GPU utilization.
  - Emits structured logs for vector generation and for fail-fast exits when CUDA hardware is missing.

## Vector Store Agent
- **Mission**: Provide a sandbox Qdrant façade that enforces 512-dimension vectors, production-parity upsert/downsert semantics, and health diagnostics.
- **Inputs**: Embeddings + payloads from the Users Kernel, along with collection config under `ArcFace:VectorStore` (endpoint, key, vector size, auto-create flag).
- **Outputs**: Upsert/downsert acknowledgements, similarity search payloads, document probes, and collection health signals used by monitoring endpoints.
- **Interfaces**: `ISandboxVectorStore` and `AddSandboxVectorStore` (documented in `subagents/vector-store/README.md`). Ensures collection creation, validation, and CRUD/search operations.
- **UI Expectations**: Feed the “Vector Store Monitor” page with collection tags, recent upsert/downsert feed entries, document probe tables, diagnostics issues, and placeholder similarity searches if the search API is not yet wired.
- **Constraints & Notes**:
  - Refuses embeddings that are not 512 floats long.
  - Maintains soft-delete payload metadata (`is_deleted`, timestamps) for parity with `Aspire-Full.VectorStore`.
  - Designed to run against local Qdrant (`http://localhost:6334`) but supports managed endpoints/API keys.

## Users Kernel Agent
- **Mission**: Mirror the production Users controller plus EF Core model within the sandbox so teams can iterate on user lifecycle flows without touching the main solution.
- **Inputs**: REST calls (CRUD + login), embeddings retrieved from the Embedding Service, configuration for sandbox DB (`ConnectionStrings:Users`).
- **Outputs**: `SandboxUserResponse` JSON payloads, persistence operations, integration events/logs that downstream automation and UI consume.
- **Interfaces**: Same HTTP surface as `Aspire_Full.Api.UsersController`, backed by DI contracts (`IArcFaceEmbeddingService`, `ISandboxVectorStore`). Seed data and integration tests reside under `tests/ArcFaceSandbox.UsersKernel.Tests`.
- **UI Expectations**: Provide data/actions for the “Users Sandbox” UI page: listing users, toggling active state, triggering upsert/update/downsert/login flows, and surfacing the vector synchronization badge per user.
- **Constraints & Notes**:
  - Must remain isolated from production databases; default SQLite/Postgres connection lives entirely inside the sandbox.
  - Keeps deterministic fixtures for automated test agents.
  - Defers Aspire AppHost integration until the sandbox contracts are approved.

## Shared Expectations Across Agents
- Communicate only via explicit interfaces or REST contracts; avoid implicit shared state.
- Each agent maintains its own `README.md` (configuration, diagnostics, extension points) before shipping code.
- Tests stay close to the owning agent; cross-agent tests aggregate under `tests/ArcFaceSandbox.UsersKernel.Tests` or future multi-agent suites.
- The semantic UI shell hosts three dedicated pages (Embedding Diagnostics, Users Sandbox, Vector Store Monitor) so stakeholders can validate each agent independently prior to Aspire integration.
