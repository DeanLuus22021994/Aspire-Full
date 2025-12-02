using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Aspire_Full.Tensor.Core;

/// <summary>
/// Centralized diagnostics for Tensor operations.
/// Provides ActivitySource for distributed tracing and Meter for metrics.
/// </summary>
public static class TensorDiagnostics
{
    /// <summary>
    /// Activity source name for OpenTelemetry tracing.
    /// </summary>
    public const string ActivitySourceName = "Aspire-Full.Tensor";

    /// <summary>
    /// Meter name for OpenTelemetry metrics.
    /// </summary>
    public const string MeterName = "Aspire.Tensor.Core";

    /// <summary>
    /// Activity source for distributed tracing of tensor operations.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Meter for tensor operation metrics.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Counter for total tensor operations.
    /// </summary>
    public static readonly Counter<long> OperationsCounter =
        Meter.CreateCounter<long>("tensor.operations.total", "operations", "Total number of tensor operations");

    /// <summary>
    /// Histogram for tensor operation duration.
    /// </summary>
    public static readonly Histogram<double> OperationDuration =
        Meter.CreateHistogram<double>("tensor.operation.duration_ms", "ms", "Duration of tensor operations");

    /// <summary>
    /// Counter for bytes processed by tensor operations.
    /// </summary>
    public static readonly Counter<long> BytesProcessed =
        Meter.CreateCounter<long>("tensor.bytes.processed", "bytes", "Bytes processed by tensor operations");
}
