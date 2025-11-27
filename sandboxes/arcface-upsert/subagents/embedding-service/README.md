# Embedding Service Sub-Agent

## Mission
Own ArcFace model execution and deliver normalized 512-float embeddings for downstream upsert/downsert usage. This agent is the only component allowed to touch ONNXRuntime APIs or model files.

## Responsibilities
- Manage model acquisition and validation (hashing, version pinning).
- Provide async APIs for single/batch embedding generation.
- Surface telemetry (latency, device info, failures) for observability.
- Offer configuration toggles for CPU vs CUDA execution providers.

## Out of Scope
- Persisting embeddings
- Calling Qdrant directly
- Handling HTTP requests

## Interfaces (Draft)
```csharp
public interface IArcFaceEmbeddingService
{
    Task<ReadOnlyMemory<float>> GenerateAsync(Stream alignedFace, CancellationToken ct = default);
    IAsyncEnumerable<ReadOnlyMemory<float>> GenerateBatchAsync(IEnumerable<Stream> faces, CancellationToken ct = default);
    ArcFaceModelInfo ModelInfo { get; }
}
```

## Open Tasks
- Define model download script contract (`scripts/get-arcface-model.ps1`).
- Decide on input preprocessing expectations (assume aligned 112x112 RGB for now).
