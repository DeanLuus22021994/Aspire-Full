# Sandbox Scripts

This directory will host automation for the ArcFace sandbox. Planned scripts include:

- `get-arcface-model.ps1` / `.sh`: Fetches InsightFace `arcface_r100_v1` ONNX weights, verifies SHA256, and stores them under `sandboxes/arcface-upsert/models/`.
- `start-qdrant.ps1`: Spins up a disposable Qdrant container configured for the sandbox collection.
- `seed-users.ps1`: Populates sample users + embeddings for manual testing.

Scripts will be added after the sandbox spec is approved.
