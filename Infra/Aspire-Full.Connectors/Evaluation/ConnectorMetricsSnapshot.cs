using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Aspire_Full.Connectors;

public sealed record ConnectorMetricSample(
    ConnectorMetricDimension Dimension,
    string Metric,
    double Score,
    ConnectorMetricStatus Status,
    string Detail,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ConnectorMetricSummary(
    ConnectorMetricDimension Dimension,
    ConnectorMetricStatus Status,
    double AverageScore,
    int Observations,
    DateTimeOffset LastUpdated,
    IReadOnlyList<ConnectorMetricSample> Samples);

public sealed record ConnectorMetricsSnapshot(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ConnectorMetricSummary> Summaries);

public interface IConnectorMetricSnapshotProvider
{
    ConnectorMetricsSnapshot BuildSnapshot(int take = 100);
}

internal sealed class ConnectorMetricSnapshotProvider : IConnectorMetricSnapshotProvider
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    private readonly IEvaluationOrchestrator _orchestrator;

    public ConnectorMetricSnapshotProvider(IEvaluationOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public ConnectorMetricsSnapshot BuildSnapshot(int take = 100)
    {
        var records = _orchestrator.GetRecent(take);
        if (records.Count == 0)
        {
            return new ConnectorMetricsSnapshot(DateTimeOffset.UtcNow, Array.Empty<ConnectorMetricSummary>());
        }

        var grouped = new Dictionary<ConnectorMetricDimension, List<ConnectorMetricSample>>();
        foreach (var record in records)
        {
            if (!TryResolveDimension(record.Metadata ?? EmptyMetadata, out var dimension))
            {
                continue;
            }

            var detail = TryGet(record.Metadata, ConnectorTraceTags.Detail);
            var sample = new ConnectorMetricSample(
                dimension,
                record.Metric,
                record.Score,
                ConnectorMetricStatusHelper.FromScore(record.Score),
                detail ?? string.Empty,
                record.Timestamp,
                record.Metadata ?? EmptyMetadata);

            if (!grouped.TryGetValue(dimension, out var list))
            {
                list = new List<ConnectorMetricSample>();
                grouped[dimension] = list;
            }

            list.Add(sample);
        }

        var summaries = new List<ConnectorMetricSummary>(grouped.Count);
        foreach (var definition in ConnectorMetricCatalog.All)
        {
            if (!grouped.TryGetValue(definition.Dimension, out var samples) || samples.Count == 0)
            {
                continue;
            }

            samples.Sort(static (left, right) => right.Timestamp.CompareTo(left.Timestamp));
            var averageScore = samples.Average(sample => sample.Score);
            summaries.Add(new ConnectorMetricSummary(
                definition.Dimension,
                ConnectorMetricStatusHelper.FromScore(averageScore),
                Math.Round(averageScore, 3),
                samples.Count,
                samples[0].Timestamp,
                new ReadOnlyCollection<ConnectorMetricSample>(samples)));
        }

        return new ConnectorMetricsSnapshot(DateTimeOffset.UtcNow, summaries);
    }

    private static bool TryResolveDimension(IReadOnlyDictionary<string, string> metadata, out ConnectorMetricDimension dimension)
    {
        dimension = default;
        if (!metadata.TryGetValue(ConnectorTraceTags.Category, out var dimensionValue))
        {
            return false;
        }

        return Enum.TryParse(dimensionValue, ignoreCase: true, out dimension);
    }

    private static string? TryGet(IReadOnlyDictionary<string, string>? metadata, string key)
    {
        if (metadata is null)
        {
            return null;
        }

        return metadata.TryGetValue(key, out var value) ? value : null;
    }
}
