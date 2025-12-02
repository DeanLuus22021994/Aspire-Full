# Embedding Service Sub-Agent

## Mission
Own ArcFace model execution and deliver normalized 512-float embeddings for downstream semantic experiences. This agent is the only component allowed to touch ONNXRuntime APIs or model files, and its outputs will be visualized alongside status metadata in the shared sandbox UI.

## Responsibilities
- Manage model acquisition and validation (hashing, version pinning).
- Provide async APIs for single/batch embedding generation (consumers can defer wiring these calls into Aspire until the API integration phase).
- Surface telemetry (latency, device info, failures) for observability.
- Enforce CUDA-only execution on Tensor Core GPUs; fail fast when NVIDIA hardware is unavailable.

## Out of Scope
- Persisting embeddings
- Calling Qdrant directly
- Handling HTTP requests
- Owning presentation logic outside of the sandbox UI page documented below

## UI Requirement (Page 1 of 3)
- Deliver an "Embedding Diagnostics" page in the sandbox semantic UI (shared with Users Kernel + Vector Store pages) showing:
    - Current model name/version, execution provider, and checksum status.
    - Realtime telemetry: average latency, queue depth, and last error (if any).
    - A sample embedding request preview (mock face input → 512-value sparkline) fetched via the public API.
- The page should read from the Embedding Service API only; no Aspire AppHost wiring is required until later integration.

## Interfaces
```csharp
public interface IArcFaceEmbeddingService
{
    Task<ReadOnlyMemory<float>> GenerateAsync(Stream alignedFace, CancellationToken ct = default);
    IAsyncEnumerable<ReadOnlyMemory<float>> GenerateBatchAsync(IEnumerable<Stream> faces, CancellationToken ct = default);
    ArcFaceModelInfo ModelInfo { get; }
}
```

### DI Registration
```csharp
builder.Services.AddArcFaceEmbedding(builder.Configuration);
```

Configuration keys live under `ArcFace:Embedding`:

| Key | Description | Default |
| --- | --- | --- |
| `ModelPath` | Absolute/relative path to `arcface_r100_v1.onnx`. Must exist and pass optional SHA check. | `./models/arcface_r100_v1.onnx` |
| `ExecutionProvider` | Must be `Cuda`; CPU/DirectML values are rejected at startup. | `Cuda` |
| `TensorCoreHeadroom` | Fractional headroom reserved when batching (0.05-0.5). | `0.1` |
| `VerifyModelChecksum` | Toggles SHA-256 verification using `ExpectedSha256`. | `true` |
| `EnableVerboseLogging` | Emits per-batch latency logs. | `false` |

> **GPU requirement**: The embedding service verifies CUDA availability during DI setup and throws if Tensor Core devices are missing. There is no CPU or DirectML fallback path.

## Open Tasks
- Define model download script contract (`scripts/get-arcface-model.ps1`).
- Decide on input preprocessing expectations (assume aligned 112x112 RGB for now).
