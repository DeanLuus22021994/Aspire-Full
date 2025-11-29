using YamlDotNet.Serialization;

namespace Aspire_Full.Pipeline.Modules.Discovery;

public class EnvironmentConfig
{
    [YamlMember(Alias = "repository", ApplyNamingConventions = false)]
    public RepositoryConfig Repository { get; set; } = new();

    [YamlMember(Alias = "dotnet", ApplyNamingConventions = false)]
    public DotNetConfig DotNet { get; set; } = new();

    [YamlMember(Alias = "python", ApplyNamingConventions = false)]
    public PythonConfig Python { get; set; } = new();

    [YamlMember(Alias = "docker", ApplyNamingConventions = false)]
    public DockerConfig Docker { get; set; } = new();

    [YamlMember(Alias = "hardware", ApplyNamingConventions = false)]
    public HardwareConfig Hardware { get; set; } = new();
}

public class RepositoryConfig
{
    [YamlMember(Alias = "root", ApplyNamingConventions = false)]
    public string Root { get; set; } = "";

    [YamlMember(Alias = "branch", ApplyNamingConventions = false)]
    public string Branch { get; set; } = "";
}

public class DotNetConfig
{
    [YamlMember(Alias = "sdk", ApplyNamingConventions = false)]
    public string Sdk { get; set; } = "";

    [YamlMember(Alias = "preview", ApplyNamingConventions = false)]
    public bool Preview { get; set; }
}

public class PythonConfig
{
    [YamlMember(Alias = "version", ApplyNamingConventions = false)]
    public string Version { get; set; } = "";

    [YamlMember(Alias = "manager", ApplyNamingConventions = false)]
    public string Manager { get; set; } = "uv";

    [YamlMember(Alias = "torch", ApplyNamingConventions = false)]
    public TorchConfig Torch { get; set; } = new();
}

public class TorchConfig
{
    [YamlMember(Alias = "device", ApplyNamingConventions = false)]
    public string Device { get; set; } = "cpu";
}

public class DockerConfig
{
    [YamlMember(Alias = "registry", ApplyNamingConventions = false)]
    public string Registry { get; set; } = "localhost:5000";

    [YamlMember(Alias = "image_prefix", ApplyNamingConventions = false)]
    public string ImagePrefix { get; set; } = "aspire-agents";

    [YamlMember(Alias = "images", ApplyNamingConventions = false)]
    public DockerImagesConfig Images { get; set; } = new();
}

public class DockerImagesConfig
{
    [YamlMember(Alias = "python", ApplyNamingConventions = false)]
    public PythonImagesConfig Python { get; set; } = new();
}

public class PythonImagesConfig
{
    [YamlMember(Alias = "free_threading", ApplyNamingConventions = false)]
    public FreeThreadingImagesConfig FreeThreading { get; set; } = new();
}

public class FreeThreadingImagesConfig
{
    [YamlMember(Alias = "bleeding_edge", ApplyNamingConventions = false)]
    public string BleedingEdge { get; set; } = "";

    [YamlMember(Alias = "stable", ApplyNamingConventions = false)]
    public string Stable { get; set; } = "";

    [YamlMember(Alias = "note", ApplyNamingConventions = false)]
    public string Note { get; set; } = "";
}

public class HardwareConfig
{
    [YamlMember(Alias = "gpu", ApplyNamingConventions = false)]
    public GpuConfig Gpu { get; set; } = new();
}

public class GpuConfig
{
    [YamlMember(Alias = "enabled", ApplyNamingConventions = false)]
    public bool Enabled { get; set; }

    [YamlMember(Alias = "driver", ApplyNamingConventions = false)]
    public string Driver { get; set; } = "";
}
