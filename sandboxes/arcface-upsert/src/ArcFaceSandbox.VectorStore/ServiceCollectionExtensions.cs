using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ArcFaceSandbox.VectorStore;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSandboxVectorStore(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = SandboxVectorStoreOptions.ConfigurationSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SandboxVectorStoreOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.VectorSize == SandboxVectorStoreOptions.DefaultVectorSize,
                "ArcFace sandbox enforces 512-d embeddings.")
            .ValidateOnStart();

        services.TryAddSingleton<IQdrantVectorClient, QdrantClientAdapter>();
        services.TryAddSingleton<ISandboxVectorStore, SandboxVectorStore>();

        return services;
    }
}
