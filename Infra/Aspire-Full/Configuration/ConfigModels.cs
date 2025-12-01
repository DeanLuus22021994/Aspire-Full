using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace Aspire_Full.Configuration;

// YAML Config Models
public class RuntimeConfig
{
    [YamlMember(Alias = "telemetry")]
    public TelemetryConfig Telemetry { get; set; } = new();

    [YamlMember(Alias = "environment")]
    public EnvironmentConfig Environment { get; set; } = new();
}

public class EnvironmentConfig
{
    [YamlMember(Alias = "dotnet")]
    public DotNetConfig DotNet { get; set; } = new();

    [YamlMember(Alias = "python")]
    public PythonConfig Python { get; set; } = new();

    [YamlMember(Alias = "hardware")]
    public HardwareConfig Hardware { get; set; } = new();
}

public class DotNetConfig
{
    [YamlMember(Alias = "sdk")]
    public string Sdk { get; set; } = "";

    [YamlMember(Alias = "preview")]
    public bool Preview { get; set; }
}

public class PythonConfig
{
    [YamlMember(Alias = "version")]
    public string Version { get; set; } = "";

    [YamlMember(Alias = "manager")]
    public string Manager { get; set; } = "";

    [YamlMember(Alias = "torch")]
    public TorchConfig Torch { get; set; } = new();
}

public class TorchConfig
{
    [YamlMember(Alias = "device")]
    public string Device { get; set; } = "cpu";
}

public class HardwareConfig
{
    [YamlMember(Alias = "gpu")]
    public GpuHardwareConfig Gpu { get; set; } = new();
}

public class GpuHardwareConfig
{
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; }
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
