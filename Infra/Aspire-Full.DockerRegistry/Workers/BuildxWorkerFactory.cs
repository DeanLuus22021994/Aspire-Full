using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Abstractions;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.DockerRegistry.Native;
using Aspire_Full.Tensor.Core.Abstractions;
using Aspire_Full.Tensor.Core.Memory;
using Aspire_Full.Tensor.Core.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.DockerRegistry.Workers;

/// <summary>
/// Factory for GPU-accelerated BuildKit workers with integrated resource monitoring.
/// Implements IDockerRegistryGpuMonitor for telemetry and memory pool management.
/// </summary>
public sealed class BuildxWorkerFactory : IBuildxWorkerFactory, IDockerRegistryGpuMonitor, IDisposable
{
    private readonly ConcurrentQueue<IBuildxWorker> _workerPool = new();
    private readonly ConcurrentQueue<IBuildxExporter> _exporterPool = new();
    private readonly SemaphoreSlim _workerSemaphore;
    private readonly SemaphoreSlim _exporterSemaphore;
    private readonly DockerRegistryOptions _options;
    private readonly ILogger<BuildxWorkerFactory> _logger;
    private readonly GpuMemoryPool _memoryPool;
    private readonly GpuProcessExecutor _processExecutor;
    private long _allocatedMemory;
    private int _currentUtilization;
    private bool _disposed;

    // Metrics
    private static readonly Meter s_meter = new("Aspire.DockerRegistry.Workers", "1.0.0");
    private static readonly Counter<long> s_workersCreated = s_meter.CreateCounter<long>("workers.created");
    private static readonly Counter<long> s_workersReused = s_meter.CreateCounter<long>("workers.reused");
    private static readonly Counter<long> s_gpuOperations = s_meter.CreateCounter<long>("gpu.operations");
    private static readonly Histogram<double> s_gpuOperationDuration = s_meter.CreateHistogram<double>("gpu.operation_duration_ms");
    private static readonly Counter<long> s_gpuBytesProcessed = s_meter.CreateCounter<long>("gpu.bytes_processed");
    private static readonly UpDownCounter<long> s_activeWorkers = s_meter.CreateUpDownCounter<long>("workers.active");

    public BuildxWorkerFactory(IOptions<DockerRegistryOptions> options, ILogger<BuildxWorkerFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
        _workerSemaphore = new SemaphoreSlim(_options.MaxWorkerPoolSize);
        // Enforce 2 exporters to 1 worker ratio
        _exporterSemaphore = new SemaphoreSlim(_options.MaxWorkerPoolSize * 2);

        // Initialize GPU memory pool
        _memoryPool = new GpuMemoryPool(
            _options.GpuAcceleration.MaxGpuMemoryPoolBuffers,
            _options.GpuAcceleration.DefaultBufferSize);

        // Initialize GPU process executor
        _processExecutor = new GpuProcessExecutor(_logger, _options.GpuAcceleration, _memoryPool);

        if (_options.GpuAcceleration.Enabled)
        {
            _logger.LogInformation(
                "GPU acceleration enabled. Bootstrap images: devel={DevelImage}, runtime={RuntimeImage}. " +
                "Memory pool: {MaxBuffers} buffers x {BufferSize:N0} bytes = {TotalMemory:N0} bytes estimated",
                _options.GpuAcceleration.CudaBootstrapDevelImage,
                _options.GpuAcceleration.CudaBootstrapRuntimeImage,
                _options.GpuAcceleration.MaxGpuMemoryPoolBuffers,
                _options.GpuAcceleration.DefaultBufferSize,
                _options.GpuAcceleration.EstimatedGpuMemoryBytes);
        }
    }

    #region IBuildxWorkerFactory Implementation

    public async Task<IBuildxWorker> GetWorkerAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _workerSemaphore.WaitAsync(cancellationToken);

        if (_workerPool.TryDequeue(out var worker))
        {
            s_workersReused.Add(1);
            s_activeWorkers.Add(1);
            return worker;
        }

        s_workersCreated.Add(1);
        s_activeWorkers.Add(1);
        return new BuildxWorker(Guid.NewGuid().ToString(), _logger, _options.GpuAcceleration);
    }

    public Task ReleaseWorkerAsync(IBuildxWorker worker)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _workerPool.Enqueue(worker);
        _workerSemaphore.Release();
        s_activeWorkers.Add(-1);
        return Task.CompletedTask;
    }

    public async Task<IBuildxExporter> GetExporterAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _exporterSemaphore.WaitAsync(cancellationToken);

        if (_exporterPool.TryDequeue(out var exporter))
        {
            return exporter;
        }

        return new BuildxExporter(Guid.NewGuid().ToString(), _logger, _options.GpuAcceleration, _memoryPool);
    }

    public Task ReleaseExporterAsync(IBuildxExporter exporter)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _exporterPool.Enqueue(exporter);
        _exporterSemaphore.Release();
        return Task.CompletedTask;
    }

    #endregion

    #region IDockerRegistryGpuMonitor Implementation

    /// <summary>
    /// Returns true if GPU compute is available.
    /// </summary>
    public bool IsGpuAvailable => NativeTensorContext.IsGpuAvailable;

    /// <summary>
    /// Gets the number of GPU devices.
    /// </summary>
    public int DeviceCount => NativeTensorContext.GpuDeviceCount;

    /// <summary>
    /// Gets current GPU utilization percentage.
    /// </summary>
    public int CurrentUtilization => _currentUtilization;

    /// <summary>
    /// Gets estimated total GPU memory based on configuration.
    /// </summary>
    public long TotalMemory => _options.GpuAcceleration.EstimatedGpuMemoryBytes;

    /// <summary>
    /// Gets currently allocated GPU memory from the pool.
    /// </summary>
    public long AllocatedMemory => Interlocked.Read(ref _allocatedMemory);

    /// <summary>
    /// Gets available GPU memory.
    /// </summary>
    public long AvailableMemory => TotalMemory - AllocatedMemory;

    /// <summary>
    /// Gets the GPU memory pool.
    /// </summary>
    public GpuMemoryPool MemoryPool => _memoryPool;

    /// <summary>
    /// Gets the GPU acceleration options.
    /// </summary>
    public GpuAccelerationOptions GpuAccelerationOptions => _options.GpuAcceleration;

    /// <summary>
    /// Gets the GPU process executor for streaming operations.
    /// </summary>
    public GpuProcessExecutor ProcessExecutor => _processExecutor;

    /// <summary>
    /// Records a GPU compute operation for metrics.
    /// </summary>
    public void RecordOperation(GpuOperationType type, double durationMs, long bytesProcessed)
    {
        s_gpuOperations.Add(1, new KeyValuePair<string, object?>("type", type.ToString()));
        s_gpuOperationDuration.Record(durationMs, new KeyValuePair<string, object?>("type", type.ToString()));
        s_gpuBytesProcessed.Add(bytesProcessed);
    }

    /// <summary>
    /// Refreshes GPU statistics from the device.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!IsGpuAvailable) return;

        // Update allocated memory from pool
        Interlocked.Exchange(ref _allocatedMemory, _memoryPool.TotalBytesAllocated);

        // Get device info for utilization
        if (NativeTensorContext.GetDeviceInfo(0, out var info) == 0)
        {
            // Estimate utilization from memory usage
            if (info.total_memory > 0)
            {
                var usedMemory = info.total_memory - info.free_memory;
                _currentUtilization = (int)((double)usedMemory / info.total_memory * 100);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets a snapshot of GPU device state.
    /// </summary>
    public async Task<GpuDeviceSnapshot> GetDeviceSnapshotAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        if (!IsGpuAvailable || deviceId >= DeviceCount)
        {
            return new GpuDeviceSnapshot
            {
                DeviceId = deviceId,
                DeviceName = "N/A",
                ComputeCapabilityMajor = 0,
                ComputeCapabilityMinor = 0,
                TotalMemory = 0,
                FreeMemory = 0,
                UsedMemory = 0,
                Utilization = 0,
                Temperature = 0,
                PowerUsage = 0,
                MultiprocessorCount = 0,
                Timestamp = DateTime.UtcNow
            };
        }

        NativeTensorContext.GetDeviceInfo(deviceId, out var info);

        await Task.CompletedTask;

        return new GpuDeviceSnapshot
        {
            DeviceId = deviceId,
            DeviceName = $"GPU {deviceId}",
            ComputeCapabilityMajor = info.compute_capability_major,
            ComputeCapabilityMinor = info.compute_capability_minor,
            TotalMemory = info.total_memory,
            FreeMemory = info.free_memory,
            UsedMemory = info.total_memory - info.free_memory,
            Utilization = _currentUtilization,
            Temperature = 0, // Would need NVML for temperature
            PowerUsage = 0,  // Would need NVML for power
            MultiprocessorCount = info.multiprocessor_count,
            Timestamp = DateTime.UtcNow
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _processExecutor.DisposeAsync();
        _memoryPool.Dispose();
        _workerSemaphore.Dispose();
        _exporterSemaphore.Dispose();
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _memoryPool.Dispose();
        _workerSemaphore.Dispose();
        _exporterSemaphore.Dispose();
    }
}
