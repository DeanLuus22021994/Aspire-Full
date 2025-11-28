using System.Collections.Concurrent;
using System.Linq;

namespace Aspire_Full.Connectors;

public interface IConnectorHealthRegistry
{
    void ReportHealthy(string name, string? detail = null, IReadOnlyDictionary<string, string>? metadata = null);
    void ReportUnhealthy(string name, string? detail = null, IReadOnlyDictionary<string, string>? metadata = null);
    IReadOnlyCollection<ConnectorHealthSnapshot> GetAll();
}

public sealed record ConnectorHealthSnapshot(
    string Name,
    bool Healthy,
    string? Detail,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset Timestamp);

internal sealed class ConnectorHealthRegistry : IConnectorHealthRegistry
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata = new Dictionary<string, string>();
    private readonly ConcurrentDictionary<string, ConnectorHealthSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public void ReportHealthy(string name, string? detail = null, IReadOnlyDictionary<string, string>? metadata = null)
        => Report(name, healthy: true, detail, metadata);

    public void ReportUnhealthy(string name, string? detail = null, IReadOnlyDictionary<string, string>? metadata = null)
        => Report(name, healthy: false, detail, metadata);

    public IReadOnlyCollection<ConnectorHealthSnapshot> GetAll() => _snapshots.Values.ToArray();

    private void Report(string name, bool healthy, string? detail, IReadOnlyDictionary<string, string>? metadata)
    {
        var snapshot = new ConnectorHealthSnapshot(
            name,
            healthy,
            detail,
            metadata ?? EmptyMetadata,
            DateTimeOffset.UtcNow);

        _snapshots.AddOrUpdate(name, snapshot, (_, _) => snapshot);
    }
}
