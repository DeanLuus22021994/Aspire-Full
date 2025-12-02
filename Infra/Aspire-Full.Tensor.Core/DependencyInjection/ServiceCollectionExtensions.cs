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
    /// Maximum number of buffers to pool. Default is 16.
    /// </summary>
    public int MaxBufferCount { get; set; } = 16;

    /// <summary>
    /// Default buffer size in bytes. Default is 64 MB.
    /// </summary>
    public nuint DefaultBufferSize { get; set; } = 64 * 1024 * 1024;

    /// <summary>
    /// Whether to prefer GPU operations when available. Default is true.
    /// </summary>
    public bool PreferGpu { get; set; } = true;

    /// <summary>
    /// Enable detailed metrics collection. Default is true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    // --- Model Registry Options ---

    /// <summary>
    /// Directory where models are cached on disk. Default is "/models".
    /// </summary>
    public string ModelCacheDirectory { get; set; } = "/models";

    /// <summary>
    /// Maximum number of models to keep in memory. Default is 10.
    /// </summary>
    public int MaxCachedModels { get; set; } = 10;

    /// <summary>
    /// Whether to track version history for models. Default is true.
    /// </summary>
    public bool TrackModelVersions { get; set; } = true;

    /// <summary>
    /// Policy for evicting models when cache is full. Default is LRU.
    /// </summary>
    public EvictionPolicy ModelEvictionPolicy { get; set; } = EvictionPolicy.Lru;
}
