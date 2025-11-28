using System.Diagnostics;

namespace Aspire_Full.Connectors;

public static class ConnectorDiagnostics
{
    public const string ActivitySourceName = "Aspire-Full.Connectors";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
