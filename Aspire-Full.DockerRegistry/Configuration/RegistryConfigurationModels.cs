using System.Collections.Generic;

namespace Aspire_Full.DockerRegistry.Configuration;

public class RegistryConfiguration
{
    public List<RegistryProviderConfig> Providers { get; set; } = new();
    public ValidationConfig Validation { get; set; } = new();
}

public class RegistryProviderConfig
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Generic"; // ACR, ECR, Generic
    public string UrlPattern { get; set; } = string.Empty;
    public Dictionary<string, string> Options { get; set; } = new();
}

public class ValidationConfig
{
    public bool OptimizeLayers { get; set; }
    public bool ValidateTensorContent { get; set; }
    public string CompressionLevel { get; set; } = "Fastest";
    public bool DeduplicationEnabled { get; set; }
    public string TensorCheckStrictness { get; set; } = "Standard";
}
