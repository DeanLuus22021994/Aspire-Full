# Sub-Agent SRP Requirements

Each sandbox component is treated as an autonomous sub-agent. The following single-responsibility specs define what each one owns, the inputs/outputs it must honor, and its future acceptance criteria.

## Embedding Service Agent
- **Responsibility**: Execute ArcFace ONNX inference on CUDA-only Tensor Core GPUs and emit normalized 512-float vectors.
- **Inputs**: Byte streams or file paths for aligned face crops; configuration for CUDA execution (device selection, Tensor Core headroom).
- **Outputs**: `ReadOnlyMemory<float>` embeddings plus metadata (model version, device, inference latency).
- **Constraints**:
  - Must verify the ONNX model hash before first use.
  - Provides async APIs for single and batched inference.
  - Emits structured logs when vectors are generated and when the service fails fast because CUDA hardware is missing.

## Vector Store Agent
- **Responsibility**: Interact with the sandbox Qdrant collection (`arcface-sandbox`) enforcing 512-dim vectors, and implement upsert/downsert/search semantics identical to production.
- **Inputs**: Embeddings + payload metadata coming from the Embedding Service and Users Kernel.
- **Outputs**: Confirmation of upsert/downsert actions, search results, and diagnostics on collection health.
- **Constraints**:
  - Must refuse embeddings that deviate from 512 dims.
  - Creates/validates the collection with cosine distance on startup.
  - Provides batch upsert APIs tuned for GPU efficiency (configurable batch size).

## Users Kernel Agent
- **Responsibility**: Mirror the Users controller + EF Core model to orchestrate user lifecycle operations (upsert, downsert, login tracking) inside the sandbox.
- **Inputs**: REST requests / integration tests along with embeddings supplied by the Embedding Service.
- **Outputs**: JSON responses, persistence updates, and events/logs describing user state transitions.
- **Constraints**:
  - No shared state with production DB; use sandbox migrations + in-memory or local Postgres.
  - Integrates with the Vector Store Agent via clearly defined interfaces (e.g., `IVectorStoreService`).
  - Provides deterministic seed data for tests.

## Shared Expectations
- Communication between agents happens through explicit interfaces to maintain loose coupling.
- Every agent must include a `README.md` documenting configuration, diagnostics, and extension points before code lands.
- Tests should be colocated with each agent when practical, but cross-agent integration tests will live under `Sandbox.Users.Tests`.
