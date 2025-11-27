namespace Aspire_Full.Qdrant;

/// <summary>
/// Strongly typed configuration for connecting to a Qdrant instance.
/// </summary>
public sealed class QdrantOptions
{
    /// <summary>
    /// Fully qualified HTTP or gRPC endpoint (e.g. http://qdrant:6334).
    /// </summary>
    public string? Endpoint { get; set; } = QdrantDefaults.DefaultEndpoint;

    /// <summary>
    /// Optional API key used when connecting to managed Qdrant clusters.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Default collection name used for semantic operations.
    /// </summary>
    public string Collection { get; set; } = QdrantDefaults.DefaultCollectionName;

    /// <summary>
    /// Vector dimensionality expected by the collection.
    /// </summary>
    public int VectorSize { get; set; } = QdrantDefaults.DefaultVectorSize;

    /// <summary>
    /// Timeout, in seconds, applied to outbound gRPC calls.
    /// </summary>
    public double GrpcTimeoutSeconds { get; set; } = QdrantDefaults.DefaultGrpcTimeoutSeconds;
}
