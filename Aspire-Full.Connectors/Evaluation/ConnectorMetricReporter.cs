using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        activity?.SetTag(ConnectorTraceTags.Category, definition.Dimension.ToString());
        activity?.SetTag(ConnectorTraceTags.Scenario, definition.Scenario);
        activity?.SetTag(ConnectorTraceTags.Status, report.Status.ToString());
        activity?.SetTag(ConnectorTraceTags.Score, Clamp(report.Score));
        if (!string.IsNullOrWhiteSpace(report.Detail))
        {
            activity?.SetTag(ConnectorTraceTags.Detail, report.Detail);
        }

        activity?.SetTag(definition.TraceTag, Clamp(report.Score));
        activity?.SetStatus(StatusCodeMap[report.Status], report.Detail);

        var metadata = report.Metadata ?? EmptyMetadata;
        var record = new EvaluationRunRecord(
            RunId: Guid.NewGuid().ToString("N"),
            Scenario: definition.Scenario,
            Metric: definition.Metric,
            Score: Clamp(report.Score),
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

    private static double Clamp(double score) => Math.Clamp(score, 0, 1);
}
