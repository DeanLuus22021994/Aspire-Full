using System.Text.Json.Serialization;

namespace Aspire_Full.Shared.Models;

public class Settings
{
    [JsonPropertyName("registry")]
    public RegistrySettings Registry { get; set; } = new();

    [JsonPropertyName("agents")]
    public AgentSettings Agents { get; set; } = new();
}

public class RegistrySettings
{
    [JsonPropertyName("port")]
    public int Port { get; set; } = 5000;

    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = "aspire-registry-data";
}

public class AgentSettings
{
    [JsonPropertyName("replicas")]
    public int Replicas { get; set; } = 1;

    [JsonPropertyName("gpu")]
    public bool Gpu { get; set; } = true;
}
