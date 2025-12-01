using Aspire_Full.VectorStore;

namespace Aspire_Full.Connectors;

public sealed class ConnectorHubOptions
{
    public const string SectionName = "Connectors";

    public VectorStoreConnectorOptions VectorStore { get; init; } = new();

    public sealed class VectorStoreConnectorOptions
    {
        public string CollectionName { get; init; } = QdrantDefaults.DefaultCollectionName;
    }
}
