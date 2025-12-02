using Microsoft.Extensions.AI;

namespace Aspire_Full.Connectors.Embeddings;

/// <summary>
/// Mock embedding generator for testing and development scenarios.
/// Returns deterministic 384-dimension vectors matching all-MiniLM-L6-v2 output format.
/// </summary>
public sealed class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const int DefaultDimension = 384;
    private readonly int _dimension;

    /// <summary>
    /// Initializes a new instance of <see cref="MockEmbeddingGenerator"/>.
    /// </summary>
    /// <param name="dimension">The dimension size for generated embeddings. Default is 384.</param>
    public MockEmbeddingGenerator(int dimension = DefaultDimension)
    {
        _dimension = dimension;
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata => new("Mock");

    /// <inheritdoc />
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = new GeneratedEmbeddings<Embedding<float>>();

        foreach (var val in values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Generate deterministic vector based on input hash for reproducibility
            var vector = new float[_dimension];
            var hash = val.GetHashCode();
            var random = new Random(hash);

            for (int i = 0; i < _dimension; i++)
            {
                vector[i] = (float)(random.NextDouble() * 2 - 1); // Range [-1, 1]
            }

            // Normalize vector to unit length
            NormalizeVector(vector);

            result.Add(new Embedding<float>(vector));
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public void Dispose() { }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    private static void NormalizeVector(float[] vector)
    {
        float magnitude = 0;
        for (int i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }
        magnitude = MathF.Sqrt(magnitude);

        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }
    }
}
