using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Aspire_Full.Connectors.Profiling;

/// <summary>
/// Portable profiler for connector operations.
/// Provides zero-host-dependency performance metrics and pain point identification.
/// </summary>
public sealed class ConnectorProfiler : IConnectorProfiler
{
    private static readonly Meter Meter = new("Aspire-Full.Connectors.Profiling", "1.0.0");

    private static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "connector.operation.duration",
        unit: "ms",
        description: "Duration of connector operations");

    private static readonly Counter<long> OperationCount = Meter.CreateCounter<long>(
        "connector.operation.count",
        unit: "{operations}",
        description: "Total connector operations");

    private static readonly Counter<long> ErrorCount = Meter.CreateCounter<long>(
        "connector.error.count",
        unit: "{errors}",
        description: "Total connector errors");

    private static readonly Histogram<double> MemoryUsage = Meter.CreateHistogram<double>(
        "connector.memory.usage",
        unit: "bytes",
        description: "Memory usage during operations");

    private static readonly Histogram<double> ResourceUtilization = Meter.CreateHistogram<double>(
        "connector.resource.utilization",
        unit: "%",
        description: "Resource utilization percentage");

    private readonly TimeProvider _timeProvider;

    public ConnectorProfiler(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Profiles an async operation with comprehensive metrics collection.
    /// </summary>
    public async Task<ProfiledResult<T>> ProfileAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        ProfileContext? context = null,
        CancellationToken ct = default)
    {
        context ??= new ProfileContext();
        var tags = new TagList
        {
            { "operation.name", operationName },
            { "connector.type", context.ConnectorType ?? "unknown" }
        };

        var startMemory = GC.GetTotalMemory(forceFullCollection: false);
        var startTimestamp = _timeProvider.GetTimestamp();

        using var activity = ConnectorDiagnostics.ActivitySource.StartActivity(
            $"Profile.{operationName}",
            ActivityKind.Internal);

        activity?.SetTag("profiler.enabled", true);

        try
        {
            var result = await operation(ct).ConfigureAwait(false);

            var elapsed = _timeProvider.GetElapsedTime(startTimestamp);
            var endMemory = GC.GetTotalMemory(forceFullCollection: false);
            var memoryDelta = endMemory - startMemory;

            // Record metrics
            OperationDuration.Record(elapsed.TotalMilliseconds, tags);
            OperationCount.Add(1, tags);
            MemoryUsage.Record(memoryDelta, tags);

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("profiler.duration_ms", elapsed.TotalMilliseconds);
            activity?.SetTag("profiler.memory_delta_bytes", memoryDelta);

            return new ProfiledResult<T>
            {
                Value = result,
                Duration = elapsed,
                MemoryDeltaBytes = memoryDelta,
                Success = true,
                OperationName = operationName,
                Timestamp = _timeProvider.GetUtcNow()
            };
        }
        catch (Exception ex)
        {
            var elapsed = _timeProvider.GetElapsedTime(startTimestamp);

            ErrorCount.Add(1, tags);
            OperationDuration.Record(elapsed.TotalMilliseconds, tags);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("profiler.error", ex.GetType().Name);

            return new ProfiledResult<T>
            {
                Value = default!,
                Duration = elapsed,
                MemoryDeltaBytes = 0,
                Success = false,
                ErrorMessage = ex.Message,
                OperationName = operationName,
                Timestamp = _timeProvider.GetUtcNow()
            };
        }
    }

    /// <summary>
    /// Records a resource utilization snapshot.
    /// </summary>
    public void RecordResourceUtilization(double percentage, string resourceType)
    {
        var tags = new TagList { { "resource.type", resourceType } };
        ResourceUtilization.Record(Math.Clamp(percentage, 0, 100), tags);
    }

    /// <summary>
    /// Identifies pain points from profiled operations.
    /// </summary>
    public PainPointAnalysis AnalyzePainPoints(IEnumerable<ProfiledResult<object>> results)
    {
        var list = results.ToList();
        if (list.Count == 0)
        {
            return new PainPointAnalysis { TotalOperations = 0 };
        }

        var durations = list.Select(r => r.Duration.TotalMilliseconds).ToList();
        var avgDuration = durations.Average();
        var maxDuration = durations.Max();
        var errorRate = list.Count(r => !r.Success) / (double)list.Count;

        var painPoints = new List<PainPoint>();

        // High latency detection
        var p95 = Percentile(durations, 0.95);
        if (p95 > 100) // > 100ms is concerning
        {
            painPoints.Add(new PainPoint
            {
                Category = PainPointCategory.HighLatency,
                Severity = p95 > 500 ? PainPointSeverity.Critical : PainPointSeverity.Warning,
                Description = $"P95 latency is {p95:F1}ms",
                SuggestedAction = "Consider caching or async batching"
            });
        }

        // High error rate detection
        if (errorRate > 0.01) // > 1% error rate
        {
            painPoints.Add(new PainPoint
            {
                Category = PainPointCategory.HighErrorRate,
                Severity = errorRate > 0.10 ? PainPointSeverity.Critical : PainPointSeverity.Warning,
                Description = $"Error rate is {errorRate:P1}",
                SuggestedAction = "Investigate error patterns and add retries"
            });
        }

        // Memory pressure detection
        var avgMemory = list.Where(r => r.MemoryDeltaBytes > 0)
            .Select(r => r.MemoryDeltaBytes)
            .DefaultIfEmpty(0)
            .Average();

        if (avgMemory > 10 * 1024 * 1024) // > 10MB average
        {
            painPoints.Add(new PainPoint
            {
                Category = PainPointCategory.MemoryPressure,
                Severity = avgMemory > 100 * 1024 * 1024 ? PainPointSeverity.Critical : PainPointSeverity.Warning,
                Description = $"Average memory delta is {avgMemory / 1024 / 1024:F1}MB",
                SuggestedAction = "Consider memory pooling or streaming"
            });
        }

        return new PainPointAnalysis
        {
            TotalOperations = list.Count,
            AverageDurationMs = avgDuration,
            MaxDurationMs = maxDuration,
            P95DurationMs = p95,
            ErrorRate = errorRate,
            AverageMemoryDeltaBytes = (long)avgMemory,
            PainPoints = painPoints
        };
    }

    private static double Percentile(List<double> sequence, double percentile)
    {
        if (sequence.Count == 0) return 0;
        sequence.Sort();
        var index = (int)Math.Ceiling(percentile * sequence.Count) - 1;
        return sequence[Math.Max(0, Math.Min(index, sequence.Count - 1))];
    }
}

/// <summary>
/// Interface for connector profiling operations.
/// </summary>
public interface IConnectorProfiler
{
    Task<ProfiledResult<T>> ProfileAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        ProfileContext? context = null,
        CancellationToken ct = default);

    void RecordResourceUtilization(double percentage, string resourceType);

    PainPointAnalysis AnalyzePainPoints(IEnumerable<ProfiledResult<object>> results);
}

/// <summary>
/// Context for profiled operations.
/// </summary>
public sealed class ProfileContext
{
    public string? ConnectorType { get; init; }
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

/// <summary>
/// Result of a profiled operation.
/// </summary>
public sealed class ProfiledResult<T>
{
    public required T Value { get; init; }
    public required TimeSpan Duration { get; init; }
    public required long MemoryDeltaBytes { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public required string OperationName { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Analysis of operation pain points.
/// </summary>
public sealed class PainPointAnalysis
{
    public required int TotalOperations { get; init; }
    public double AverageDurationMs { get; init; }
    public double MaxDurationMs { get; init; }
    public double P95DurationMs { get; init; }
    public double ErrorRate { get; init; }
    public long AverageMemoryDeltaBytes { get; init; }
    public IReadOnlyList<PainPoint> PainPoints { get; init; } = [];
}

/// <summary>
/// Individual pain point identified by profiling.
/// </summary>
public sealed class PainPoint
{
    public required PainPointCategory Category { get; init; }
    public required PainPointSeverity Severity { get; init; }
    public required string Description { get; init; }
    public string? SuggestedAction { get; init; }
}

/// <summary>
/// Categories of pain points.
/// </summary>
public enum PainPointCategory
{
    HighLatency,
    HighErrorRate,
    MemoryPressure,
    ResourceContention,
    GpuBottleneck,
    IoBlocking
}

/// <summary>
/// Severity levels for pain points.
/// </summary>
public enum PainPointSeverity
{
    Info,
    Warning,
    Critical
}
