# ArcFace Upsert Sandbox

Experimental workspace for validating ArcFace-based embeddings against the Aspire upsert/downsert pipeline without touching the production solution. The sandbox is self-contained, pins Qdrant vector size to 512 dimensions (matching `arcface_r100_v1`), and documents each sub-agent's single responsibility so future automation can plug in cleanly.

## Goals
- Mirror the Users API soft-delete semantics in isolation for rapid iteration
- Exercise ArcFace ONNX inference on CUDA-only Tensor Core hardware before sharing weights with the main app
- Validate upsert/downsert flows into a sandbox Qdrant collection using the reduced 512-dim schema
- Provide explicit specs so autonomous agents can own their slice of work without overlap

> **GPU requirement**: Every sandbox sub-agent now fails fast if CUDA Tensor Core devices are unavailable. There are no CPU or DirectML escape hatches, so provision NVIDIA hardware before running any scripts.

## Layout
```
sandboxes/arcface-upsert/
├── README.md                # This file
├── specs/                   # High-level sandbox & SRP specs
├── subagents/
│   ├── embedding-service/   # ArcFace ONNX runtime harness
│   ├── vector-store/        # Qdrant wiring pinned to 512 dims
│   └── users-kernel/        # Sandbox EF Core + controller surface
└── scripts/                 # Helper scripts (model download, seed data, etc.)
```

## Next Steps
1. Review `specs/subagents-srp.md` to confirm responsibilities
2. Approve the sandbox foundation
3. Begin implementing each sub-agent according to its spec
