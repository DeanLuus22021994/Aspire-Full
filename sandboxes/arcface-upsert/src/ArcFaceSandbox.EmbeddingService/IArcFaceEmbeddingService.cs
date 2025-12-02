using System.Collections.Generic;

namespace ArcFaceSandbox.EmbeddingService;

/// <summary>
/// ArcFace-specific embedding generator for aligned facial crops.
/// Implementations must normalize the output vectors to unit length.
/// </summary>
public interface IArcFaceEmbeddingService
{
    /// <summary>
    /// Generate a single embedding for the supplied, aligned face stream.
    /// </summary>
    /// <param name="alignedFace">An aligned 112x112 RGB face crop.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ReadOnlyMemory<float>> GenerateAsync(Stream alignedFace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for a batch of aligned faces.
    /// </summary>
    /// <param name="alignedFaces">Aligned 112x112 RGB face crops.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<ReadOnlyMemory<float>> GenerateBatchAsync(IEnumerable<Stream> alignedFaces, CancellationToken cancellationToken = default);

    /// <summary>
    /// Metadata describing the currently loaded ArcFace ONNX model.
    /// </summary>
    ArcFaceModelInfo ModelInfo { get; }
}
