using Aspire_Full.Agents.Core.Catalog;
using Aspire_Full.Agents.Core.Maintenance;
using Aspire_Full.Agents.Core.Services;
using Aspire_Full.Shared.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Agents.Core services.
/// </summary>
public static class AgentsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds Agents.Core services to the service collection.
    /// Registers ISubagentCatalog, ISubagentSelfReviewService, and IMaintenanceAgent.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentsCore(this IServiceCollection services)
    {
        services.TryAddSingleton<ISubagentCatalog, SubagentCatalog>();
        services.TryAddSingleton<ISubagentSelfReviewService, SubagentSelfReviewService>();
        services.TryAddSingleton<IMaintenanceAgent, MaintenanceAgent>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Adds Agents.Core services with a custom TimeProvider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="timeProvider">Custom TimeProvider instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentsCore(this IServiceCollection services, TimeProvider timeProvider)
    {
        services.TryAddSingleton(timeProvider);
        services.TryAddSingleton<ISubagentCatalog, SubagentCatalog>();
        services.TryAddSingleton<ISubagentSelfReviewService, SubagentSelfReviewService>();
        services.TryAddSingleton<IMaintenanceAgent, MaintenanceAgent>();

        return services;
    }
}
