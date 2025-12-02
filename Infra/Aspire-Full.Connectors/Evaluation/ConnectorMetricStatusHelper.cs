namespace Aspire_Full.Connectors;

internal static class ConnectorMetricStatusHelper
{
    public static ConnectorMetricStatus FromScore(double score)
    {
        if (score >= 0.8)
        {
            return ConnectorMetricStatus.Pass;
        }

        if (score >= 0.5)
        {
            return ConnectorMetricStatus.Warning;
        }

        return ConnectorMetricStatus.Fail;
    }
}
