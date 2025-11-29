using YamlDotNet.Serialization;

namespace Aspire_Full.Pipeline.Modules.Discovery;

public class RootEnvironmentConfig
{
    [YamlMember(Alias = "version", ApplyNamingConventions = false)]
    public int Version { get; set; } = 1;

    [YamlMember(Alias = "environment", ApplyNamingConventions = false)]
    public EnvironmentConfig Environment { get; set; } = new();
}

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

// Python Lint Configuration Models
public class PythonLintConfig
{
    [YamlMember(Alias = "line_length", ApplyNamingConventions = false)]
    public int LineLength { get; set; } = 100;

    [YamlMember(Alias = "vendor_globs", ApplyNamingConventions = false)]
    public List<string> VendorGlobs { get; set; } = new();

    [YamlMember(Alias = "paths", ApplyNamingConventions = false)]
    public LintPathsConfig Paths { get; set; } = new();

    [YamlMember(Alias = "flake8", ApplyNamingConventions = false)]
    public Flake8Config Flake8 { get; set; } = new();

    [YamlMember(Alias = "pycodestyle", ApplyNamingConventions = false)]
    public PyCodeStyleConfig PyCodeStyle { get; set; } = new();

    [YamlMember(Alias = "pylint", ApplyNamingConventions = false)]
    public PylintConfig Pylint { get; set; } = new();

    [YamlMember(Alias = "pyright", ApplyNamingConventions = false)]
    public PyrightConfig Pyright { get; set; } = new();

    [YamlMember(Alias = "runner", ApplyNamingConventions = false)]
    public RunnerConfig Runner { get; set; } = new();
}

public class LintPathsConfig
{
    [YamlMember(Alias = "lint_roots", ApplyNamingConventions = false)]
    public List<string> LintRoots { get; set; } = new();

    [YamlMember(Alias = "exclude_globs", ApplyNamingConventions = false)]
    public List<string> ExcludeGlobs { get; set; } = new();
}

public class Flake8Config
{
    [YamlMember(Alias = "extend_ignore", ApplyNamingConventions = false)]
    public List<string> ExtendIgnore { get; set; } = new();

    [YamlMember(Alias = "exclude", ApplyNamingConventions = false)]
    public List<string> Exclude { get; set; } = new();
}

public class PyCodeStyleConfig
{
    [YamlMember(Alias = "ignore", ApplyNamingConventions = false)]
    public List<string> Ignore { get; set; } = new();
}

public class PylintConfig
{
    [YamlMember(Alias = "disable", ApplyNamingConventions = false)]
    public List<string> Disable { get; set; } = new();

    [YamlMember(Alias = "ignore", ApplyNamingConventions = false)]
    public List<string> Ignore { get; set; } = new();

    [YamlMember(Alias = "ignore_paths", ApplyNamingConventions = false)]
    public List<string> IgnorePaths { get; set; } = new();

    [YamlMember(Alias = "ignore_patterns", ApplyNamingConventions = false)]
    public List<string> IgnorePatterns { get; set; } = new();
}

public class PyrightConfig
{
    [YamlMember(Alias = "exclude", ApplyNamingConventions = false)]
    public List<string> Exclude { get; set; } = new();
}

public class RunnerConfig
{
    [YamlMember(Alias = "auto_targets", ApplyNamingConventions = false)]
    public List<string> AutoTargets { get; set; } = new();

    [YamlMember(Alias = "pylint_disable", ApplyNamingConventions = false)]
    public List<string> PylintDisable { get; set; } = new();
}
