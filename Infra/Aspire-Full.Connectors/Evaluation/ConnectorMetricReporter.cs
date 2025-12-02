using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Connectors;

public interface IConnectorMetricReporter
{
    Task ReportAsync(ConnectorMetricReport report, CancellationToken cancellationToken = default);
}

internal sealed class ConnectorMetricReporter : IConnectorMetricReporter
{
    private static readonly IReadOnlyDictionary<ConnectorMetricStatus, ActivityStatusCode> StatusCodeMap = new Dictionary<ConnectorMetricStatus, ActivityStatusCode>
    {
        [ConnectorMetricStatus.Pass] = ActivityStatusCode.Ok,
        [ConnectorMetricStatus.Warning] = ActivityStatusCode.Ok,
        [ConnectorMetricStatus.Fail] = ActivityStatusCode.Error
    };

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata = new Dictionary<string, string>();

    private readonly IEvaluationOrchestrator _orchestrator;
    private readonly ILogger<ConnectorMetricReporter> _logger;

    public ConnectorMetricReporter(IEvaluationOrchestrator orchestrator, ILogger<ConnectorMetricReporter> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task ReportAsync(ConnectorMetricReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        var definition = ConnectorMetricCatalog.GetDefinition(report.Dimension);
        var activityName = $"ConnectorMetricReporter.{definition.Dimension}";
        using var activity = ConnectorDiagnostics.ActivitySource.StartActivity(activityName);

        var normalizedScore = Clamp(report.Score);
        activity?.SetTag(ConnectorTraceTags.Category, definition.Dimension.ToString());
        activity?.SetTag(ConnectorTraceTags.Scenario, definition.Scenario);
        activity?.SetTag(ConnectorTraceTags.Status, report.Status.ToString());
        activity?.SetTag(ConnectorTraceTags.Score, normalizedScore);
        if (!string.IsNullOrWhiteSpace(report.Detail))
        {
            activity?.SetTag(ConnectorTraceTags.Detail, report.Detail);
        }

        activity?.SetTag(definition.TraceTag, normalizedScore);
        activity?.SetStatus(StatusCodeMap[report.Status], report.Detail);

        var metadata = BuildRecordMetadata(report, definition, normalizedScore);
        var record = new EvaluationRunRecord(
            RunId: Guid.NewGuid().ToString("N"),
            Scenario: definition.Scenario,
            Metric: definition.Metric,
            Score: normalizedScore,
            ModelId: metadata.TryGetValue("model_id", out var modelId) ? modelId : null,
            Timestamp: DateTimeOffset.UtcNow,
            Metadata: metadata);

        try
        {
            await _orchestrator.RecordAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to record connector metric {Metric}", definition.Metric);
            throw;
        }
    }

    private static IReadOnlyDictionary<string, string> BuildRecordMetadata(ConnectorMetricReport report, ConnectorMetricDefinition definition, double normalizedScore)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (report.Metadata is { Count: > 0 })
        {
            foreach (var pair in report.Metadata)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        metadata[ConnectorTraceTags.Category] = definition.Dimension.ToString();
        metadata[ConnectorTraceTags.Scenario] = definition.Scenario;
        metadata[ConnectorTraceTags.Status] = report.Status.ToString();
        metadata[ConnectorTraceTags.Score] = normalizedScore.ToString("0.###", CultureInfo.InvariantCulture);
        metadata[ConnectorTraceTags.MetricName] = definition.Metric;
        if (!string.IsNullOrWhiteSpace(report.Detail))
        {
            metadata[ConnectorTraceTags.Detail] = report.Detail!;
        }

        return metadata;
    }

    private static double Clamp(double score) => Math.Clamp(score, 0, 1);
}
