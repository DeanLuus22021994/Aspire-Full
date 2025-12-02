using Aspire_Full.Tensor.Core.Compute;
using Aspire_Full.Tensor.Core.Orchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire_Full.Tensor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTensorRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TensorModelCatalogOptions>()
            .Bind(configuration.GetSection("TensorModels"))
            .ValidateOnStart();

        services.AddScoped<ITensorRuntimeService, TensorRuntimeService>();

        // TensorComputeService is now registered via AddTensorCore() in Infra
        return services;
    }

    public static IServiceCollection AddTensorOrchestration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TensorModelCatalogOptions>()
            .Bind(configuration.GetSection("TensorModels"))
            .ValidateOnStart();

        // Job store and coordinator are now registered via AddTensorCore() in Infra
        // This method is kept for backward compatibility
        services.AddSingleton<ITensorJobStore, InMemoryTensorJobStore>();
        services.AddSingleton<ITensorJobCoordinator, TensorJobCoordinator>();

        return services;
    }
}
