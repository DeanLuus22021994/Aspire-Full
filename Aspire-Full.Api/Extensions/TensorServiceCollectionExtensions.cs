using Aspire_Full.Api.Tensor;
using Aspire_Full.Tensor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire_Full.Api.Extensions;

public static class TensorServiceCollectionExtensions
{
    public static IServiceCollection AddTensorOrchestration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TensorModelCatalogOptions>()
            .Bind(configuration.GetSection("TensorModels"))
            .ValidateOnStart();

        services.AddSingleton<ITensorJobStore, InMemoryTensorJobStore>();
        services.AddSingleton<ITensorVectorBridge, TensorVectorBridge>();
        services.AddSingleton<ITensorJobCoordinator, TensorJobCoordinator>();

        return services;
    }
}
