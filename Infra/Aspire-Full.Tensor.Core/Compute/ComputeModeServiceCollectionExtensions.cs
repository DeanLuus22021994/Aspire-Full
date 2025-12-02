using Aspire_Full.Tensor.Core.Compute;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering compute mode services.
/// </summary>
public static class ComputeModeServiceCollectionExtensions
{
    /// <summary>
    /// Adds compute mode service with GPU offloading support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional configuration section for compute options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddComputeMode(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        if (configuration != null)
        {
            services.Configure<ComputeOptions>(configuration.GetSection("compute"));
        }
        else
        {
            services.Configure<ComputeOptions>(_ => { });
        }

        services.AddSingleton<IComputeModeService, ComputeModeService>();

        return services;
    }

    /// <summary>
    /// Adds compute mode service with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for compute options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddComputeMode(
        this IServiceCollection services,
        Action<ComputeOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IComputeModeService, ComputeModeService>();

        return services;
    }

    /// <summary>
    /// Adds compute mode service configured for full GPU offload (no CPU compute).
    /// </summary>
    public static IServiceCollection AddFullGpuCompute(this IServiceCollection services)
    {
        return services.AddComputeMode(options =>
        {
            options.Mode = ComputeMode.Gpu;
            options.OffloadStrategy = OffloadStrategy.Full;
            options.FallbackToCpu = false;
        });
    }

    /// <summary>
    /// Adds compute mode service configured for hybrid compute with CPU fallback.
    /// </summary>
    public static IServiceCollection AddHybridCompute(this IServiceCollection services)
    {
        return services.AddComputeMode(options =>
        {
            options.Mode = ComputeMode.Hybrid;
            options.OffloadStrategy = OffloadStrategy.Selective;
            options.FallbackToCpu = true;
        });
    }
}
