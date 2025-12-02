using ArcFaceSandbox.EmbeddingService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ArcFaceSandbox.EmbeddingService.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddArcFaceEmbedding_BindsOptionsAndResolvesService()
    {
        var tempModelPath = Path.GetTempFileName();
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"ArcFace:Embedding:ModelPath"] = tempModelPath,
                    [$"ArcFace:Embedding:MaxBatchSize"] = "4"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IArcFaceInferenceRunner, FakeRunner>();
            services.AddArcFaceEmbedding(configuration);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<ArcFaceEmbeddingOptions>>().Value;
            Assert.Equal(tempModelPath, options.ModelPath);

            var embeddingService = provider.GetRequiredService<IArcFaceEmbeddingService>();
            await using var faceStream = await CreateAlignedFaceAsync();
            var vector = await embeddingService.GenerateAsync(faceStream);

            Assert.Equal(512, vector.Length);
        }
        finally
        {
            File.Delete(tempModelPath);
        }
    }

    private static async Task<MemoryStream> CreateAlignedFaceAsync()
    {
        using var image = new Image<Rgb24>(ArcFacePreprocessor.TargetSize, ArcFacePreprocessor.TargetSize, new Rgb24(100, 50, 25));
        var stream = new MemoryStream();
        await image.SaveAsync(stream, new PngEncoder());
        stream.Position = 0;
        return stream;
    }

    private sealed class FakeRunner : IArcFaceInferenceRunner
    {
        public ArcFaceModelInfo ModelInfo { get; } = new("test", "1.0", "cuda", "n/a", DateTime.UtcNow);

        public float[] Run(string inputName, DenseTensor<float> batchTensor)
        {
            var batchSize = batchTensor.Dimensions[0];
            var output = new float[batchSize * 512];
            // deterministic output so normalization is stable
            for (var i = 0; i < output.Length; i++)
            {
                output[i] = 1f;
            }

            return output;
        }

        public void Dispose()
        {
        }
    }
}
