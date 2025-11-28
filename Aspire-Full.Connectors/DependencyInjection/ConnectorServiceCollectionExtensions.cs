using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddSingleton<IConnectorHealthRegistry, ConnectorHealthRegistry>();
        services.AddSingleton<IEvaluationOrchestrator, InMemoryEvaluationOrchestrator>();
        services.AddSingleton<IConnectorMetricReporter, ConnectorMetricReporter>();
        services.AddSingleton<IVectorStoreConnector, VectorStoreConnector>();

        services.AddOpenTelemetry().WithTracing(builder => builder.AddSource(ConnectorDiagnostics.ActivitySourceName));

        return services;
    }
}
