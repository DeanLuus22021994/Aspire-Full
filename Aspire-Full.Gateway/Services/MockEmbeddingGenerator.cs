using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Aspire_Full.Gateway.Services;

// Mock generator for now to ensure build success
public class MockEmbeddingGenerator : Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>
{
    public Microsoft.Extensions.AI.EmbeddingGeneratorMetadata Metadata => new("Mock");

    public Task<Microsoft.Extensions.AI.GeneratedEmbeddings<Microsoft.Extensions.AI.Embedding<float>>> GenerateAsync(IEnumerable<string> values, Microsoft.Extensions.AI.EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = new Microsoft.Extensions.AI.GeneratedEmbeddings<Microsoft.Extensions.AI.Embedding<float>>();
        foreach (var val in values)
        {
            // Return random 384-dim vector (matching all-MiniLM-L6-v2)
            var vector = new float[384];
            Random.Shared.NextBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(vector.AsSpan()));
            result.Add(new Microsoft.Extensions.AI.Embedding<float>(vector));
        }
        return Task.FromResult(result);
    }

    public void Dispose() { }
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
