namespace ArcFaceSandbox.EmbeddingService;

/// <summary>
/// Describes runtime metadata for the loaded ArcFace model.
/// </summary>
public sealed record ArcFaceModelInfo(
    string ModelName,
    string ModelVersion,
    string ExecutionProvider,
    string Sha256,
    DateTime LoadedAtUtc,
    int VectorSize = 512,
    int InputImageSize = 112);
