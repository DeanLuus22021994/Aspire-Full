using System.Collections.Generic;

namespace Aspire_Full.Tensor;

public sealed record TensorModelDescriptor
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string Digest { get; init; } = string.Empty;
    public string ModelUri { get; init; } = string.Empty;
    public string DocumentationUri { get; init; } = string.Empty;
    public int EmbeddingSize { get; init; } = 0;
    public IList<int> InputShape { get; init; } = new List<int>();
    public IList<string> PreferredExecutionProviders { get; init; } = new List<string>();
    public IList<string> AlternateExecutionProviders { get; init; } = new List<string>();
}

public sealed record TensorCapabilityResponse
{
    public required bool SupportsWebGpu { get; init; }
    public required bool SupportsWebGl2 { get; init; }
    public required bool SupportsSimd { get; init; }
    public required string RecommendedExecutionProvider { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record TensorModelCatalogOptions
{
    public IList<TensorModelDescriptor> Models { get; init; } = new List<TensorModelDescriptor>();
}
