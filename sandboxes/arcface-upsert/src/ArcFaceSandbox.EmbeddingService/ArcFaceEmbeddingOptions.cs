using System.ComponentModel.DataAnnotations;

namespace ArcFaceSandbox.EmbeddingService;

/// <summary>
/// Configuration for the ArcFace embedding service.
/// </summary>
public sealed class ArcFaceEmbeddingOptions
{
    private const int DefaultMaxBatchSize = 64;

    [Required]
    public string ModelPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "models", "arcface_r100_v1.onnx");

    /// <summary>
    /// Optional SHA256 checksum that will be verified before loading the model.
    /// </summary>
    public string? ExpectedSha256 { get; set; }

    /// <summary>
    /// CUDA device index to target when GPU execution is enabled.
    /// </summary>
    [Range(0, 15)]
    public int CudaDeviceId { get; set; }

    /// <summary>
    /// Maximum batch size submitted to the ONNX runtime.
    /// </summary>
    [Range(1, 512)]
    public int MaxBatchSize { get; set; } = DefaultMaxBatchSize;

    /// <summary>
    /// Minimum GPU headroom to preserve when scheduling tensor-core work.
    /// Expressed as a percentage (0-1). Default 0.1 (10% headroom).
    /// </summary>
    [Range(0.05, 0.5)]
    public double TensorCoreHeadroom { get; set; } = 0.1;

    /// <summary>
    /// Controls whether SHA verification is enforced at startup.
    /// </summary>
    public bool VerifyModelChecksum { get; set; } = true;

    /// <summary>
    /// Enables aggressive logging for debugging.
    /// </summary>
    public bool EnableVerboseLogging { get; set; }
}
