using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace Aspire_Full.Qdrant;

/// <summary>
/// Dependency injection helpers for wiring up Qdrant services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQdrantClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection(QdrantDefaults.ConfigurationSectionName))
            .PostConfigure(options =>
            {
                options.Endpoint ??= QdrantDefaults.DefaultEndpoint;
                if (string.IsNullOrWhiteSpace(options.Collection))
                {
                    options.Collection = QdrantDefaults.DefaultCollectionName;
                }

                if (options.VectorSize <= 0)
                {
                    options.VectorSize = QdrantDefaults.DefaultVectorSize;
                }

                if (options.GrpcTimeoutSeconds <= 0)
                {
                    options.GrpcTimeoutSeconds = QdrantDefaults.DefaultGrpcTimeoutSeconds;
                }
            });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<QdrantOptions>>().Value;
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

            if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
            {
                throw new InvalidOperationException($"Invalid Qdrant endpoint '{options.Endpoint}'.");
            }

            var timeout = TimeSpan.FromSeconds(options.GrpcTimeoutSeconds);
            return new QdrantClient(endpoint, options.ApiKey, timeout, loggerFactory);
        });

        return services;
    }
}
