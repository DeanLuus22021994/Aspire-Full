using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;

namespace Aspire_Full.Embeddings.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmbeddingServices(this IServiceCollection services)
    {
        services.AddSingleton<IEmbeddingService, EmbeddingService>();

        // Register OnnxEmbeddingGenerator with GPU support
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OnnxEmbeddingGenerator>>();
            var modelPath = Path.Combine(AppContext.BaseDirectory, "models", "all-MiniLM-L6-v2.onnx");
            var vocabPath = Path.Combine(AppContext.BaseDirectory, "models", "vocab.txt");

            // Ensure models directory exists
            var modelDir = Path.GetDirectoryName(modelPath);
            if (!Directory.Exists(modelDir))
            {
                Directory.CreateDirectory(modelDir!);
            }

            // Check if model exists
            if (!File.Exists(modelPath) || !File.Exists(vocabPath))
            {
                logger.LogCritical("ONNX Model or Vocab not found at {ModelPath}. Please download 'all-MiniLM-L6-v2.onnx' and 'vocab.txt' to this location.", modelPath);
                // Fallback to mock to keep the app running
                return new MockEmbeddingGenerator();
            }

            return new OnnxEmbeddingGenerator(modelPath, vocabPath, logger);
        });

        return services;
    }
}
