using Aspire_Full.Tensor.Core.Memory;
using Aspire_Full.Tensor.Core.Native;

namespace Aspire_Full.Tensor.Core.Abstractions;

/// <summary>
/// Interface for tensor compute runtime providing GPU/CPU abstraction.
/// Implementations can optimize for specific hardware capabilities.
/// </summary>
public interface ITensorRuntime : IAsyncDisposable
{
    /// <summary>
    /// Gets whether GPU compute is available.
    /// </summary>
    bool IsGpuAvailable { get; }

    /// <summary>
    /// Gets the number of GPU devices.
    /// </summary>
    int DeviceCount { get; }

    /// <summary>
    /// Gets the GPU memory pool for buffer management.
    /// </summary>
    GpuMemoryPool MemoryPool { get; }

    /// <summary>
    /// Computes cosine similarity between two vectors.
    /// </summary>
    float CosineSimilarity(ReadOnlySpan<float> x, ReadOnlySpan<float> y);

    /// <summary>
    /// Computes L2 norm of a vector.
    /// </summary>
    float Norm(ReadOnlySpan<float> x);

    /// <summary>
    /// Computes dot product of two vectors.
    /// </summary>
    float Dot(ReadOnlySpan<float> x, ReadOnlySpan<float> y);

    /// <summary>
    /// Applies softmax activation.
    /// </summary>
    void SoftMax(ReadOnlySpan<float> x, Span<float> destination);

    /// <summary>
    /// Applies ReLU activation.
    /// </summary>
    void ReLU(ReadOnlySpan<float> x, Span<float> destination);

    /// <summary>
    /// Performs matrix multiplication: C = A * B.
    /// </summary>
    void MatrixMultiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c, int m, int n, int k);

    /// <summary>
    /// Performs mean pooling for transformer embeddings.
    /// </summary>
    void MeanPooling(ReadOnlySpan<float> input, ReadOnlySpan<long> attentionMask, Span<float> output, int batchSize, int seqLen, int hiddenSize);

    /// <summary>
    /// Validates tensor data against a threshold.
    /// </summary>
    bool ValidateContent(ReadOnlySpan<float> data, float threshold, out NativeTensorContext.TensorMetrics metrics);

    /// <summary>
    /// Gets device information for a specific GPU.
    /// </summary>
    NativeTensorContext.GpuDeviceInfo? GetDeviceInfo(int deviceId);
}

/// <summary>
/// Interface for monitoring GPU resources with telemetry integration.
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
    MemoryTransfer,
    MatrixMultiply,
    MeanPooling,
    Activation,
    Embedding
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
