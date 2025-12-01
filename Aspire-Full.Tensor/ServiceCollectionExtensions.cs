using Aspire_Full.Tensor.Services;
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
        services.AddScoped<ITensorComputeService, TensorComputeService>();

        return services;
    }

    public static IServiceCollection AddTensorOrchestration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TensorModelCatalogOptions>()
            .Bind(configuration.GetSection("TensorModels"))
            .ValidateOnStart();

        services.AddSingleton<ITensorJobStore, InMemoryTensorJobStore>();
        services.AddSingleton<ITensorJobCoordinator, TensorJobCoordinator>();

        return services;
    }
}
