namespace Aspire_Full.Shared.Abstractions;

/// <summary>
/// Interface for maintenance agents that perform workspace upkeep tasks.
/// Supports GPU-accelerated operations for tensor-optimized builds.
/// </summary>
public interface IMaintenanceAgent
{
    /// <summary>
    /// Runs maintenance tasks on the specified workspace.
    /// </summary>
    /// <param name="workspaceRoot">Root directory of the workspace to maintain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure with error details.</returns>
    Task<Result<MaintenanceResult>> RunAsync(string workspaceRoot, CancellationToken ct = default);
}

/// <summary>
/// Result of a maintenance operation.
/// </summary>
public sealed record MaintenanceResult
{
    /// <summary>
    /// Tasks that were executed.
    /// </summary>
    public required IReadOnlyList<string> ExecutedTasks { get; init; }

    /// <summary>
    /// Duration of the maintenance operation.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Whether GPU acceleration was used.
    /// </summary>
    public required bool GpuAccelerated { get; init; }
}
