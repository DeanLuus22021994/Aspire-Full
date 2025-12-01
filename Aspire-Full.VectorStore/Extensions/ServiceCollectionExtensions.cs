using Aspire_Full.Qdrant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire_Full.VectorStore.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVectorStore(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Qdrant Client
        services.AddQdrantClient(configuration);

        // Register Vector Store Service
        services.AddSingleton<IVectorStoreService, QdrantVectorStoreService>();

        return services;
    }
}
