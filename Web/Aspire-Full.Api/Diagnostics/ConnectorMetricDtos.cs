using System.Linq;
using Aspire_Full.Connectors;

namespace Aspire_Full.Api.Diagnostics;

public sealed record ConnectorMetricSampleDto(
    string Metric,
    double Score,
    string Status,
    DateTimeOffset Timestamp,
    string Detail,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ConnectorMetricSummaryDto(
    string Dimension,
    string Status,
    double AverageScore,
    int Observations,
    DateTimeOffset LastUpdated,
    IReadOnlyList<ConnectorMetricSampleDto> Samples);

public sealed record ConnectorMetricsResponseDto(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ConnectorMetricSummaryDto> Summaries);

internal static class ConnectorMetricDtoMapper
{
    public static ConnectorMetricsResponseDto FromSnapshot(ConnectorMetricsSnapshot snapshot)
    {
        var summaries = snapshot.Summaries
            .Select(summary => new ConnectorMetricSummaryDto(
                summary.Dimension.ToString(),
                summary.Status.ToString(),
                summary.AverageScore,
                summary.Observations,
                summary.LastUpdated,
                summary.Samples
                    .Select(sample => new ConnectorMetricSampleDto(
                        sample.Metric,
                        sample.Score,
                        sample.Status.ToString(),
                        sample.Timestamp,
                        sample.Detail,
                        sample.Metadata))
                    .ToList()))
            .ToList();

        return new ConnectorMetricsResponseDto(snapshot.GeneratedAt, summaries);
    }
}
