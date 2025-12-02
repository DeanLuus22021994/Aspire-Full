using Aspire_Full.Connectors.Embeddings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Connectors.DependencyInjection;

/// <summary>
/// Extension methods for registering embedding services with dependency injection.
/// </summary>
public static class EmbeddingServiceCollectionExtensions
{
    /// <summary>
    /// Registers embedding services with ONNX GPU-accelerated generator.
    /// Falls back to mock generator if model files are not found.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="modelPath">Path to the ONNX model file. Defaults to models/all-MiniLM-L6-v2.onnx.</param>
    /// <param name="vocabPath">Path to the vocabulary file. Defaults to models/vocab.txt.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEmbeddingServices(
        this IServiceCollection services,
        string? modelPath = null,
        string? vocabPath = null)
    {
        services.AddSingleton<IEmbeddingService, EmbeddingService>();

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OnnxEmbeddingGenerator>>();

            var resolvedModelPath = modelPath ?? Path.Combine(AppContext.BaseDirectory, "models", "all-MiniLM-L6-v2.onnx");
            var resolvedVocabPath = vocabPath ?? Path.Combine(AppContext.BaseDirectory, "models", "vocab.txt");

            // Ensure models directory exists
            var modelDir = Path.GetDirectoryName(resolvedModelPath);
            if (!string.IsNullOrEmpty(modelDir) && !Directory.Exists(modelDir))
            {
                Directory.CreateDirectory(modelDir);
            }

            // Check if model exists
            if (!File.Exists(resolvedModelPath) || !File.Exists(resolvedVocabPath))
            {
                logger.LogWarning(
                    "ONNX Model or Vocab not found at {ModelPath}. Falling back to MockEmbeddingGenerator.",
                    resolvedModelPath);
                return new MockEmbeddingGenerator();
            }

            return new OnnxEmbeddingGenerator(resolvedModelPath, resolvedVocabPath, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers embedding services with a mock generator for testing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dimension">The dimension size for mock embeddings. Default is 384.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMockEmbeddingServices(this IServiceCollection services, int dimension = 384)
    {
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ => new MockEmbeddingGenerator(dimension));
        services.AddSingleton<IEmbeddingService, EmbeddingService>();

        return services;
    }
}
