using Aspire_Full.Tensor.Core;
using Aspire_Full.Tensor.Core.Abstractions;
using Aspire_Full.Tensor.Core.Compute;
using Aspire_Full.Tensor.Core.Memory;
using Aspire_Full.Tensor.Core.Models;
using Aspire_Full.Tensor.Core.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Tensor.Core services.
/// </summary>
public static class TensorCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds Tensor.Core services to the service collection.
    /// Registers ITensorRuntime, IGpuResourceMonitor, and IModelRegistry as singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTensorCore(this IServiceCollection services)
    {
        return services.AddTensorCore(configure: null);
    }

    /// <summary>
    /// Adds Tensor.Core services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTensorCore(
        this IServiceCollection services,
        Action<TensorCoreOptions>? configure)
    {
        var options = new TensorCoreOptions();
        configure?.Invoke(options);

        // Register ComputeModeService for GPU/CPU/Hybrid mode toggling
        services.Configure<ComputeOptions>(computeOptions =>
        {
            computeOptions.Mode = options.ComputeMode;
            computeOptions.OffloadStrategy = options.OffloadStrategy;
            computeOptions.FallbackToCpu = options.FallbackToCpu;
            computeOptions.GpuDeviceId = options.GpuDeviceId;
            computeOptions.MemoryFraction = options.MemoryFraction;
            computeOptions.AllowGrowth = options.AllowGrowth;
            computeOptions.EnableDynamicBatching = options.EnableDynamicBatching;
            computeOptions.MaxBatchSize = options.MaxBatchSize;
            computeOptions.BatchTimeoutMs = options.BatchTimeoutMs;
        });
        services.TryAddSingleton<IComputeModeService, ComputeModeService>();

        // Register the memory pool as singleton
        services.TryAddSingleton(sp =>
            new GpuMemoryPool(options.MaxBufferCount, options.DefaultBufferSize));

        // Register TensorRuntime as both ITensorRuntime and IGpuResourceMonitor
        services.TryAddSingleton<TensorRuntime>();
        services.TryAddSingleton<ITensorRuntime>(sp => sp.GetRequiredService<TensorRuntime>());
        services.TryAddSingleton<IGpuResourceMonitor>(sp => sp.GetRequiredService<TensorRuntime>());

        // Register ModelRegistry with configuration
        services.Configure<ModelRegistryOptions>(registryOptions =>
        {
            registryOptions.CacheDirectory = options.ModelCacheDirectory;
            registryOptions.MaxCachedModels = options.MaxCachedModels;
            registryOptions.EvictionPolicy = options.ModelEvictionPolicy;
            registryOptions.TrackVersions = options.TrackModelVersions;
        });
        services.TryAddSingleton<IModelRegistry, ModelRegistry>();

        // Register Compute services
        services.TryAddSingleton<ITensorComputeService, TensorComputeService>();

        // Register Orchestration services
        services.TryAddSingleton<ITensorJobStore, InMemoryTensorJobStore>();

        return services;
    }

    /// <summary>
    /// Adds Tensor.Core services with GPU memory pool configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="maxBufferCount">Maximum number of pooled GPU buffers.</param>
    /// <param name="defaultBufferSize">Default buffer size in bytes.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTensorCore(
        this IServiceCollection services,
        int maxBufferCount,
        nuint defaultBufferSize)
    {
        return services.AddTensorCore(options =>
        {
            options.MaxBufferCount = maxBufferCount;
            options.DefaultBufferSize = defaultBufferSize;
        });
    }
}

/// <summary>
/// Configuration options for Tensor.Core services.
/// </summary>
public sealed class TensorCoreOptions
{
    /// <summary>
    /// Maximum number of buffers to pool. Default is 32 for 9GB RAM.
    /// </summary>
    public int MaxBufferCount { get; set; } = 32;

    /// <summary>
    /// Default buffer size in bytes. Default is 128 MB for tensor operations.
    /// </summary>
    public nuint DefaultBufferSize { get; set; } = 128 * 1024 * 1024;

    /// <summary>
    /// Whether to prefer GPU operations when available. Default is true.
    /// </summary>
    public bool PreferGpu { get; set; } = true;

    /// <summary>
    /// Enable detailed metrics collection. Default is true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    // --- Compute Mode Options ---

    /// <summary>
    /// Compute execution mode. Default is Hybrid for flexibility.
    /// </summary>
    public ComputeMode ComputeMode { get; set; } = ComputeMode.Hybrid;

    /// <summary>
    /// Strategy for offloading operations. Default is Full.
    /// </summary>
    public OffloadStrategy OffloadStrategy { get; set; } = OffloadStrategy.Full;

    /// <summary>
    /// Whether to fallback to CPU when GPU fails. Default is true for resilience.
    /// </summary>
    public bool FallbackToCpu { get; set; } = true;

    /// <summary>
    /// GPU device ID to use. Default is 0.
    /// </summary>
    public int GpuDeviceId { get; set; } = 0;

    /// <summary>
    /// Fraction of GPU memory to allocate (0.0-1.0). Default is 0.9 for performance.
    /// </summary>
    public double MemoryFraction { get; set; } = 0.9;

    /// <summary>
    /// Allow GPU memory to grow dynamically. Default is true.
    /// </summary>
    public bool AllowGrowth { get; set; } = true;

    /// <summary>
    /// Enable dynamic batching for throughput. Default is true.
    /// </summary>
    public bool EnableDynamicBatching { get; set; } = true;

    /// <summary>
    /// Maximum batch size for batched operations. Default is 64 for 9GB RAM.
    /// </summary>
    public int MaxBatchSize { get; set; } = 64;

    /// <summary>
    /// Batch timeout in milliseconds before forcing execution. Default is 25.
    /// </summary>
    public int BatchTimeoutMs { get; set; } = 25;

    // --- Model Registry Options ---

    /// <summary>
    /// Directory where models are cached on disk. Uses shared mount. Default is "/shared/models".
    /// </summary>
    public string ModelCacheDirectory { get; set; } = "/shared/models";

    /// <summary>
    /// Maximum number of models to keep in memory. Default is 20 for 9GB RAM.
    /// </summary>
    public int MaxCachedModels { get; set; } = 20;

    /// <summary>
    /// Whether to track version history for models. Default is true.
    /// </summary>
    public bool TrackModelVersions { get; set; } = true;

    /// <summary>
    /// Policy for evicting models when cache is full. Default is LRU.
    /// </summary>
    public EvictionPolicy ModelEvictionPolicy { get; set; } = EvictionPolicy.Lru;
}
