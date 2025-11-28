# ArcFace Sandbox Specification

## Scope
Create a standalone .NET 10 solution dedicated to testing ArcFace (InsightFace `arcface_r100_v1`) embeddings inside the Aspire upsert/downsert workflow. The sandbox must:
- Require zero coupling to the main `Aspire-Full` solution beyond documented contracts.
- Pin Qdrant collections to **512-dimension** cosine vectors.
- Provide scriptable model acquisition (PowerShell + Bash) with checksum validation.
- Document resource assumptions (CUDA Tensor Core GPUs required, no CPU baseline) and env vars.

## Architectural Pillars
1. **Isolation** – no shared databases, caches, or queues. Sandbox components use their own containers or in-memory stores.
2. **Determinism** – every run is reproducible via scripts committed in `scripts/`.
3. **Observability** – each sub-agent logs inputs/outputs that affect upsert/downsert results.
4. **Extensibility** – sub-agents expose minimal interfaces so they can be swapped or scaled independently.

## Deliverables
- `ArcFaceSandbox.sln` referencing three projects (`Sandbox.Users`, `Sandbox.VectorStore`, `Sandbox.Embeddings`).
- Test project `Sandbox.Users.Tests` covering controller + embedding integration (mockable ONNX outputs allowed).
- Scripts for model download, Qdrant seeding, and environment bootstrapping.
- Documentation updates (README + specs) describing how to run and extend the sandbox.

## Open Questions
- Preferred containerization strategy (Aspire AppHost vs docker-compose) for the sandbox? (Default: local dotnet run + docker compose for Qdrant.)
- Need for CI integration, or keep workbench local-only until stabilized?
