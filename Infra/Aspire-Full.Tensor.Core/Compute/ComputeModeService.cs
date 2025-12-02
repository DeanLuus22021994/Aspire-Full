using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Tensor.Core.Compute;

/// <summary>
/// Compute execution mode for tensor operations.
/// </summary>
public enum ComputeMode
{
    /// <summary>All operations offloaded to GPU. Zero CPU-bound compute.</summary>
    Gpu,

    /// <summary>All operations on CPU. Fallback mode when GPU unavailable.</summary>
    Cpu,

    /// <summary>Dynamic routing based on operation type and load.</summary>
    Hybrid
}

/// <summary>
/// Strategy for offloading compute operations.
/// </summary>
public enum OffloadStrategy
{
    /// <summary>Full offload - all tensor/embedding ops go to compute service.</summary>
    Full,

    /// <summary>Selective offload - only heavy operations go to compute service.</summary>
    Selective,

    /// <summary>Local execution with GPU acceleration.</summary>
    Local
}

/// <summary>
/// Configuration for compute mode and offloading behavior.
/// Optimized for GPU tensor compute with 9GB host RAM.
/// </summary>
public sealed class ComputeOptions
{
    /// <summary>Current compute mode. Default Hybrid for flexibility.</summary>
    public ComputeMode Mode { get; set; } = ComputeMode.Hybrid;

    /// <summary>Strategy for offloading operations to compute service.</summary>
    public OffloadStrategy OffloadStrategy { get; set; } = OffloadStrategy.Full;

    /// <summary>Whether to fallback to CPU when GPU fails. Default TRUE for resilience.</summary>
    public bool FallbackToCpu { get; set; } = true;

    /// <summary>GPU device ID to use.</summary>
    public int GpuDeviceId { get; set; } = 0;

    /// <summary>Fraction of GPU memory to allocate (0.0-1.0). Aggressive for performance.</summary>
    public double MemoryFraction { get; set; } = 0.90;

    /// <summary>Allow GPU memory to grow dynamically.</summary>
    public bool AllowGrowth { get; set; } = true;

    /// <summary>Enable dynamic batching for throughput.</summary>
    public bool EnableDynamicBatching { get; set; } = true;

    /// <summary>Maximum batch size for batched operations. Increased for 9GB RAM.</summary>
    public int MaxBatchSize { get; set; } = 64;

    /// <summary>Batch timeout in milliseconds before forcing execution.</summary>
    public int BatchTimeoutMs { get; set; } = 25;

    /// <summary>Host memory limit in bytes (9GB = 9,663,676,416).</summary>
    public long HostMemoryLimitBytes { get; set; } = 9L * 1024 * 1024 * 1024;

    /// <summary>Enable CUDA Unified Memory for host-device memory sharing.</summary>
    public bool EnableUnifiedMemory { get; set; } = true;

    /// <summary>Pin host memory for faster GPU transfers.</summary>
    public bool PinHostMemory { get; set; } = true;
}

/// <summary>
/// Event raised when compute mode changes at runtime.
/// </summary>
public sealed class ComputeModeChangedEventArgs : EventArgs
{
    public ComputeMode PreviousMode { get; init; }
    public ComputeMode NewMode { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Service for managing compute mode with real-time toggle capability.
/// Ensures all tensor/embedding operations are properly offloaded to GPU compute.
/// </summary>
public interface IComputeModeService
{
    /// <summary>Gets the current compute mode.</summary>
    ComputeMode CurrentMode { get; }

    /// <summary>Gets the current offload strategy.</summary>
    OffloadStrategy CurrentStrategy { get; }

    /// <summary>Gets whether GPU is currently available and healthy.</summary>
    bool IsGpuAvailable { get; }

    /// <summary>Event raised when compute mode changes.</summary>
    event EventHandler<ComputeModeChangedEventArgs>? ModeChanged;

    /// <summary>
    /// Toggles the compute mode at runtime.
    /// </summary>
    /// <param name="newMode">The new compute mode.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>True if mode was successfully changed.</returns>
    Task<bool> SetModeAsync(ComputeMode newMode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the offload strategy at runtime.
    /// </summary>
    /// <param name="strategy">The new offload strategy.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>True if strategy was successfully changed.</returns>
    Task<bool> SetStrategyAsync(OffloadStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that an operation should be offloaded based on current settings.
    /// </summary>
    /// <param name="operationType">The type of operation to check.</param>
    /// <returns>True if the operation should be offloaded to compute service.</returns>
    bool ShouldOffload(OperationType operationType);

    /// <summary>
    /// Gets current compute statistics.
    /// </summary>
    ComputeStatistics GetStatistics();
}

/// <summary>
/// Types of compute operations for offload decision making.
/// </summary>
public enum OperationType
{
    TensorMatMul,
    TensorConvolution,
    TensorPooling,
    EmbeddingGeneration,
    VectorSearch,
    ModelInference,
    BatchProcessing
}

/// <summary>
/// Statistics about compute operations.
/// </summary>
public sealed record ComputeStatistics
{
    public ComputeMode CurrentMode { get; init; }
    public OffloadStrategy CurrentStrategy { get; init; }
    public long TotalOperations { get; init; }
    public long GpuOperations { get; init; }
    public long CpuOperations { get; init; }
    public long OffloadedOperations { get; init; }
    public double GpuUtilization { get; init; }
    public long GpuMemoryUsedBytes { get; init; }
    public long GpuMemoryTotalBytes { get; init; }
    public DateTimeOffset LastModeChange { get; init; }
}

/// <summary>
/// Implementation of compute mode service with real-time toggle capability.
/// </summary>
public sealed class ComputeModeService : IComputeModeService, IDisposable
{
    private readonly ILogger<ComputeModeService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _modeLock = new(1, 1);

    private ComputeMode _currentMode;
    private OffloadStrategy _currentStrategy;
    private bool _gpuAvailable;
    private DateTimeOffset _lastModeChange;

    // Statistics tracking
    private long _totalOperations;
    private long _gpuOperations;
    private long _cpuOperations;
    private long _offloadedOperations;

    public event EventHandler<ComputeModeChangedEventArgs>? ModeChanged;

    public ComputeModeService(
        IOptions<ComputeOptions> options,
        ILogger<ComputeModeService> logger,
        TimeProvider timeProvider)
    {
        var opts = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;

        _currentMode = opts.Mode;
        _currentStrategy = opts.OffloadStrategy;
        _lastModeChange = _timeProvider.GetUtcNow();

        // Probe GPU availability at startup
        _gpuAvailable = ProbeGpuAvailability();

        if (_currentMode == ComputeMode.Gpu && !_gpuAvailable)
        {
            if (opts.FallbackToCpu)
            {
                _currentMode = ComputeMode.Cpu;
                _logger.LogWarning("GPU not available, falling back to CPU mode");
            }
            else
            {
                _logger.LogError("GPU mode requested but GPU not available and fallback disabled");
                throw new InvalidOperationException("GPU not available and FallbackToCpu is disabled");
            }
        }

        _logger.LogInformation(
            "ComputeModeService initialized: Mode={Mode}, Strategy={Strategy}, GPU={GpuAvailable}",
            _currentMode, _currentStrategy, _gpuAvailable);
    }

    public ComputeMode CurrentMode => _currentMode;
    public OffloadStrategy CurrentStrategy => _currentStrategy;
    public bool IsGpuAvailable => _gpuAvailable;

    public async Task<bool> SetModeAsync(ComputeMode newMode, CancellationToken cancellationToken = default)
    {
        await _modeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_currentMode == newMode)
            {
                _logger.LogDebug("Compute mode already set to {Mode}", newMode);
                return true;
            }

            // Validate GPU mode is possible
            if (newMode == ComputeMode.Gpu && !_gpuAvailable)
            {
                _logger.LogWarning("Cannot switch to GPU mode - GPU not available");
                return false;
            }

            var previousMode = _currentMode;
            _currentMode = newMode;
            _lastModeChange = _timeProvider.GetUtcNow();

            _logger.LogInformation(
                "Compute mode changed: {Previous} -> {New}",
                previousMode, newMode);

            // Raise event for listeners
            ModeChanged?.Invoke(this, new ComputeModeChangedEventArgs
            {
                PreviousMode = previousMode,
                NewMode = newMode,
                Timestamp = _lastModeChange
            });

            return true;
        }
        finally
        {
            _modeLock.Release();
        }
    }

    public async Task<bool> SetStrategyAsync(OffloadStrategy strategy, CancellationToken cancellationToken = default)
    {
        await _modeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _currentStrategy = strategy;
            _logger.LogInformation("Offload strategy changed to {Strategy}", strategy);
            return true;
        }
        finally
        {
            _modeLock.Release();
        }
    }

    public bool ShouldOffload(OperationType operationType)
    {
        Interlocked.Increment(ref _totalOperations);

        // CPU mode = never offload
        if (_currentMode == ComputeMode.Cpu)
        {
            Interlocked.Increment(ref _cpuOperations);
            return false;
        }

        // GPU mode with full offload = always offload
        if (_currentMode == ComputeMode.Gpu && _currentStrategy == OffloadStrategy.Full)
        {
            Interlocked.Increment(ref _gpuOperations);
            Interlocked.Increment(ref _offloadedOperations);
            return true;
        }

        // Selective offload - only heavy operations
        if (_currentStrategy == OffloadStrategy.Selective)
        {
            var shouldOffload = operationType switch
            {
                OperationType.TensorMatMul => true,
                OperationType.TensorConvolution => true,
                OperationType.EmbeddingGeneration => true,
                OperationType.ModelInference => true,
                OperationType.BatchProcessing => true,
                OperationType.TensorPooling => false, // Light operation
                OperationType.VectorSearch => false,  // Handled by Qdrant
                _ => false
            };

            if (shouldOffload)
            {
                Interlocked.Increment(ref _gpuOperations);
                Interlocked.Increment(ref _offloadedOperations);
            }
            else
            {
                Interlocked.Increment(ref _cpuOperations);
            }

            return shouldOffload;
        }

        // Hybrid mode - dynamic decision (simplified)
        if (_currentMode == ComputeMode.Hybrid)
        {
            // Always prefer GPU for tensor operations
            Interlocked.Increment(ref _gpuOperations);
            Interlocked.Increment(ref _offloadedOperations);
            return true;
        }

        // Local strategy = no offload
        Interlocked.Increment(ref _cpuOperations);
        return false;
    }

    public ComputeStatistics GetStatistics()
    {
        return new ComputeStatistics
        {
            CurrentMode = _currentMode,
            CurrentStrategy = _currentStrategy,
            TotalOperations = Interlocked.Read(ref _totalOperations),
            GpuOperations = Interlocked.Read(ref _gpuOperations),
            CpuOperations = Interlocked.Read(ref _cpuOperations),
            OffloadedOperations = Interlocked.Read(ref _offloadedOperations),
            GpuUtilization = GetGpuUtilization(),
            GpuMemoryUsedBytes = GetGpuMemoryUsed(),
            GpuMemoryTotalBytes = GetGpuMemoryTotal(),
            LastModeChange = _lastModeChange
        };
    }

    private bool ProbeGpuAvailability()
    {
        try
        {
            // Check CUDA_VISIBLE_DEVICES environment variable
            var cudaDevices = Environment.GetEnvironmentVariable("CUDA_VISIBLE_DEVICES");
            if (cudaDevices == "-1")
            {
                return false;
            }

            // Check NVIDIA_VISIBLE_DEVICES
            var nvidiaDevices = Environment.GetEnvironmentVariable("NVIDIA_VISIBLE_DEVICES");
            if (nvidiaDevices == "none")
            {
                return false;
            }

            // On Windows, check for nvidia-smi
            if (OperatingSystem.IsWindows())
            {
                var nvidiaSmiPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");

                if (File.Exists(nvidiaSmiPath))
                {
                    return true;
                }

                // Also check in System32 for newer drivers
                nvidiaSmiPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "nvidia-smi.exe");

                return File.Exists(nvidiaSmiPath);
            }

            // On Linux, check /dev/nvidia0
            if (OperatingSystem.IsLinux())
            {
                return File.Exists("/dev/nvidia0") || Directory.Exists("/proc/driver/nvidia");
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to probe GPU availability");
            return false;
        }
    }

    private double GetGpuUtilization()
    {
        // Placeholder - would integrate with NVML or nvidia-smi
        return 0.0;
    }

    private long GetGpuMemoryUsed()
    {
        // Placeholder - would integrate with NVML
        return 0;
    }

    private long GetGpuMemoryTotal()
    {
        // Placeholder - would integrate with NVML
        return 0;
    }

    public void Dispose()
    {
        _modeLock.Dispose();
    }
}
