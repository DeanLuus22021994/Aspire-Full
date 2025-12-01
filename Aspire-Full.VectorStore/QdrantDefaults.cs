namespace Aspire_Full.VectorStore;

/// <summary>
/// Provides shared constants for configuring Qdrant services.
/// </summary>
public static class QdrantDefaults
{
    public const string ConfigurationSectionName = "Qdrant";
    public const string DefaultCollectionName = "aspire-full-vectors";
    public const string DefaultEndpoint = "http://qdrant:6334";
    public const int DefaultVectorSize = 1536;
    public const double DefaultGrpcTimeoutSeconds = 30;
}
