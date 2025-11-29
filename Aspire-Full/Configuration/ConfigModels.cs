using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace Aspire_Full.Configuration;

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

// YAML Config Models
public class RuntimeConfig
{
    [YamlMember(Alias = "telemetry")]
    public TelemetryConfig Telemetry { get; set; } = new();
}

public class TelemetryConfig
{
    [YamlMember(Alias = "gpu")]
    public GpuConfig Gpu { get; set; } = new();
}

public class GpuConfig
{
    [YamlMember(Alias = "snapshot")]
    public GpuSnapshot Snapshot { get; set; } = new();
}

public class GpuSnapshot
{
    [YamlMember(Alias = "target_utilization")]
    public double TargetUtilization { get; set; }
}
