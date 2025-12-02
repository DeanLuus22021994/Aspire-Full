using Aspire_Full.Connectors.Abstractions;
using Aspire_Full.Connectors.Profiling;
using Aspire_Full.Tensor.Core.Orchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace Aspire_Full.Connectors;

public static class ConnectorServiceCollectionExtensions
{
    public static IServiceCollection AddConnectorHub(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ConnectorHubOptions>()
            .Bind(configuration.GetSection(ConnectorHubOptions.SectionName))
            .ValidateDataAnnotations();

        // Core services
        services.AddSingleton<IConnectorHealthRegistry, ConnectorHealthRegistry>();
        services.AddSingleton<IEvaluationOrchestrator, InMemoryEvaluationOrchestrator>();
        services.AddSingleton<IConnectorMetricReporter, ConnectorMetricReporter>();
        services.AddSingleton<IConnectorMetricSnapshotProvider, ConnectorMetricSnapshotProvider>();
        services.AddSingleton<IVectorStoreConnector, VectorStoreConnector>();
        services.AddSingleton<ITensorVectorBridge, TensorVectorBridge>();

        // Profiling
        services.TryAddSingleton<IConnectorProfiler, ConnectorProfiler>();
        services.TryAddSingleton(TimeProvider.System);

        // Portable model runner (zero host dependency - uses singleton HttpClient)
        services.TryAddSingleton<IPortableModelRunner>(sp =>
        {
            var timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
            return new HttpModelRunner("http://localhost:12434", timeProvider);
        });

        services.AddOpenTelemetry().WithTracing(builder => builder.AddSource(ConnectorDiagnostics.ActivitySourceName));

        return services;
    }

    /// <summary>
    /// Adds the connector profiler with custom configuration.
    /// </summary>
    public static IServiceCollection AddConnectorProfiler(
        this IServiceCollection services,
        TimeProvider? timeProvider = null)
    {
        services.TryAddSingleton(timeProvider ?? TimeProvider.System);
        services.TryAddSingleton<IConnectorProfiler, ConnectorProfiler>();
        return services;
    }

    /// <summary>
    /// Adds a portable model runner with custom endpoint.
    /// </summary>
    public static IServiceCollection AddPortableModelRunner(
        this IServiceCollection services,
        string baseUrl = "http://localhost:12434")
    {
        services.TryAddSingleton<IPortableModelRunner>(sp =>
        {
            var timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
            return new HttpModelRunner(baseUrl, timeProvider);
        });
        return services;
    }
}
