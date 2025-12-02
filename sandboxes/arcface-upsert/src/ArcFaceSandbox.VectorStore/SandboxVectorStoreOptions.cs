using System.ComponentModel.DataAnnotations;

namespace ArcFaceSandbox.VectorStore;

/// <summary>
/// Configuration surface for the sandbox vector store layer.
/// </summary>
public sealed class SandboxVectorStoreOptions
{
    public const string ConfigurationSectionName = "ArcFace:VectorStore";
    public const int DefaultVectorSize = 512;
    public const string DefaultCollectionName = "arcface-sandbox";
    public const string DefaultEndpoint = "http://localhost:6334";

    /// <summary>
    /// Qdrant endpoint (HTTP or gRPC).
    /// </summary>
    [Url]
    public string Endpoint { get; set; } = DefaultEndpoint;

    /// <summary>
    /// Optional API key for managed clusters.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Collection name used for sandbox operations.
    /// </summary>
    [Required]
    public string CollectionName { get; set; } = DefaultCollectionName;

    /// <summary>
    /// Expected vector size. Defaults to 512 for ArcFace.
    /// </summary>
    [Range(DefaultVectorSize, 4096)]
    public int VectorSize { get; set; } = DefaultVectorSize;

    /// <summary>
    /// Toggle collection auto-creation at startup.
    /// </summary>
    public bool AutoCreateCollection { get; set; } = true;
}
