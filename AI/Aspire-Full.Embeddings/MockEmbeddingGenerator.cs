using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Aspire_Full.Embeddings;

// Mock generator for now to ensure build success
public class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public EmbeddingGeneratorMetadata Metadata => new("Mock");

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var val in values)
        {
            // Return random 384-dim vector (matching all-MiniLM-L6-v2)
            var vector = new float[384];
            Random.Shared.NextBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(vector.AsSpan()));
            result.Add(new Embedding<float>(vector));
        }
        return Task.FromResult(result);
    }

    public void Dispose() { }
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
