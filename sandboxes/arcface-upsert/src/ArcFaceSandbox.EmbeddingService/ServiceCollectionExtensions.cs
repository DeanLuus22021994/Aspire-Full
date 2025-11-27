using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ArcFaceSandbox.EmbeddingService;

/// <summary>
/// Dependency injection helpers for wiring the ArcFace embedding service.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string DefaultConfigSection = "ArcFace:Embedding";

    public static IServiceCollection AddArcFaceEmbedding(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = DefaultConfigSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ArcFaceEmbeddingOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ModelPath), "ModelPath is required.")
            .Validate(options => File.Exists(options.ModelPath), "ArcFace model file must exist.")
            .ValidateOnStart();

        services.TryAddSingleton<IArcFaceInferenceRunner, OnnxArcFaceInferenceRunner>();
        services.TryAddSingleton<IArcFaceEmbeddingService, ArcFaceEmbeddingService>();

        return services;
    }
}
