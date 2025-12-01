using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Native;

namespace Aspire_Full.DockerRegistry.Abstractions;

/// <summary>
/// Interface for monitoring GPU resources during build operations.
/// Provides telemetry integration with System.Diagnostics.Metrics.
/// </summary>
public interface IGpuResourceMonitor : IAsyncDisposable
{
    /// <summary>
    /// Gets whether GPU acceleration is available.
    /// </summary>
    bool IsGpuAvailable { get; }

    /// <summary>
    /// Gets the number of available GPU devices.
    /// </summary>
    int DeviceCount { get; }

    /// <summary>
    /// Gets current GPU utilization as a percentage (0-100).
    /// </summary>
    int CurrentUtilization { get; }

    /// <summary>
    /// Gets the total GPU memory in bytes.
    /// </summary>
    long TotalMemory { get; }

    /// <summary>
    /// Gets the currently allocated GPU memory in bytes.
    /// </summary>
    long AllocatedMemory { get; }

    /// <summary>
    /// Gets the available GPU memory in bytes.
    /// </summary>
    long AvailableMemory { get; }

    /// <summary>
    /// Gets the GPU memory pool for buffer management.
    /// </summary>
    GpuMemoryPool MemoryPool { get; }

    /// <summary>
    /// Records a GPU compute operation for metrics.
    /// </summary>
    void RecordOperation(GpuOperationType type, double durationMs, long bytesProcessed);

    /// <summary>
    /// Refreshes GPU statistics from the device.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed device information for a specific GPU.
    /// </summary>
    Task<GpuDeviceSnapshot> GetDeviceSnapshotAsync(int deviceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Types of GPU compute operations for metrics tracking.
/// </summary>
public enum GpuOperationType
{
    TensorValidation,
    HashComputation,
    Compression,
    LayerProcessing,
    BuildExecution,
    MemoryTransfer
}

/// <summary>
/// Snapshot of GPU device state at a point in time.
/// </summary>
public readonly record struct GpuDeviceSnapshot
{
    public required int DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public required int ComputeCapabilityMajor { get; init; }
    public required int ComputeCapabilityMinor { get; init; }
    public required long TotalMemory { get; init; }
    public required long FreeMemory { get; init; }
    public required long UsedMemory { get; init; }
    public required int Utilization { get; init; }
    public required int Temperature { get; init; }
    public required int PowerUsage { get; init; }
    public required int MultiprocessorCount { get; init; }
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the compute capability as a version string (e.g., "8.6").
    /// </summary>
    public string ComputeCapability => $"{ComputeCapabilityMajor}.{ComputeCapabilityMinor}";

    /// <summary>
    /// Gets the memory utilization as a percentage.
    /// </summary>
    public double MemoryUtilization => TotalMemory > 0 ? (double)UsedMemory / TotalMemory * 100 : 0;
}
