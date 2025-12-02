using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Aspire_Full.Connectors;

public enum ConnectorMetricDimension
{
    EfCore,
    CodeQuality,
    NamedVolumeEfficiency,
    Redundancy,
    ResourceUtilization,
    CodebasePollution,
    VectorStoreQuality,
    TestingEfficiency,
    AlertingReliability,
    GpuOnlyAlerting,
    CpuComputationAnalysis
}

public enum ConnectorMetricStatus
{
    Pass,
    Warning,
    Fail
}

public sealed record ConnectorMetricDefinition(
    ConnectorMetricDimension Dimension,
    string Scenario,
    string Metric,
    string TraceTag,
    string Description);

public sealed record ConnectorMetricReport(
    ConnectorMetricDimension Dimension,
    double Score,
    ConnectorMetricStatus Status,
    string Detail,
    IReadOnlyDictionary<string, string>? Metadata = null);

public static class ConnectorMetricCatalog
{
    private static readonly IReadOnlyDictionary<ConnectorMetricDimension, ConnectorMetricDefinition> DefinitionLookup;
    public static IReadOnlyCollection<ConnectorMetricDefinition> All { get; }

    static ConnectorMetricCatalog()
    {
        var definitions = new[]
        {
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.EfCore,
                "Traceability",
                "EF Core Health",
                ConnectorTraceTags.EfCore,
                "Captures EF Core query latency and resiliency for connector-backed persistence."),
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.CodeQuality,
                "Evaluation",
                "Code Quality",
                ConnectorTraceTags.CodeQuality,
                "Measures prompt/code cleanliness to ensure embeddings originate from high quality sources."),
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.NamedVolumeEfficiency,
                "Evaluation",
                "Named Volume Offloading",
                ConnectorTraceTags.NamedVolumeEfficiency,
                "Tracks whether embeddings leverage named volume offloading and how efficiently data is flushed."),
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.Redundancy,
                "Traceability",
                "Redundancy",
                ConnectorTraceTags.Redundancy,
                "Records redundancy factors so connectors can detect drift or insufficient replication."),
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.ResourceUtilization,
                "Evaluation",
                "Resource Utilization",
                ConnectorTraceTags.ResourceUtilization,
                "Scales workloads based on compute/memory pressure to ensure efficient connector usage."),
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.CodebasePollution,
                "Traceability",
                "Codebase Pollution",
                ConnectorTraceTags.CodebasePollution,
                "Flags when noisy or low quality payloads pollute downstream connectors."),
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.VectorStoreQuality,
                "Evaluation",
                "Vector Store Quality",
                ConnectorTraceTags.VectorStoreQuality,
                "Evaluates embedding variance, load health, and Qdrant upsert success for traceability."),
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.TestingEfficiency,
                "Evaluation",
                "Testing Efficiency",
                ConnectorTraceTags.TestingEfficiency,
                "Captures how quickly connector scenarios hit test acceptance thresholds."),
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.AlertingReliability,
                "Traceability",
                "Alerting",
                ConnectorTraceTags.Alerting,
                "Monitors alerting latency and delivery success for connector incidents."),
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.GpuOnlyAlerting,
                "Traceability",
                "GPU Only Flag",
                ConnectorTraceTags.GpuOnly,
                "Surfaces GPU-only workload flags to guard against CPU fallbacks."),
            new ConnectorMetricDefinition(
                ConnectorMetricDimension.CpuComputationAnalysis,
                "Evaluation",
                "CPU Computation Analysis",
                ConnectorTraceTags.CpuComputation,
                "Quantifies CPU cost and batching effectiveness for connector operations.")
        };

        DefinitionLookup = definitions.ToDictionary(definition => definition.Dimension);
        All = new ReadOnlyCollection<ConnectorMetricDefinition>(definitions);
    }

    public static ConnectorMetricDefinition GetDefinition(ConnectorMetricDimension dimension)
    {
        if (!DefinitionLookup.TryGetValue(dimension, out var definition))
        {
            throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unknown connector metric dimension.");
        }

        return definition;
    }
}
