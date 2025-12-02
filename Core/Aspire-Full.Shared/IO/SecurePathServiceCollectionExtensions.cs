using System.IO.Abstractions;
using Aspire_Full.Shared.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering secure path services with dependency injection.
/// </summary>
public static class SecurePathServiceCollectionExtensions
{
    /// <summary>
    /// Adds the secure path service for jail-protected file access to the shared mount.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for shared storage options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSecurePathService(
        this IServiceCollection services,
        Action<SharedStorageOptions>? configure = null)
    {
        services.AddSingleton<IFileSystem, FileSystem>();

        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<SharedStorageOptions>(_ => { });
        }

        services.AddSingleton<ISecurePathService, SecurePathService>();

        return services;
    }

    /// <summary>
    /// Adds the secure path service with a custom host path.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="hostMountPath">The host mount path (e.g., "C:\SHARED" or "/shared").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSecurePathService(
        this IServiceCollection services,
        string hostMountPath)
    {
        return services.AddSecurePathService(options =>
        {
            options.HostMountPath = hostMountPath;
        });
    }
}
