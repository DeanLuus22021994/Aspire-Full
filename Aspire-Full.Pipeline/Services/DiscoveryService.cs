using Aspire_Full.Pipeline.Modules.Discovery.Components;
using Aspire_Full.Pipeline.Modules.Discovery;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Aspire_Full.Pipeline.Constants;

namespace Aspire_Full.Pipeline.Services;

public class DiscoveryService
{
    private readonly IDiscoveryComponent[] _components;

    public DiscoveryService()
    {
        _components =
        [
            new RepoComponent(),
            new DotNetComponent(),
            new PythonComponent(),
            new DockerComponent(),
            new DockerImagesComponent(),
            new HardwareComponent()
        ];
    }

    public async Task RunDiscoveryAsync()
    {
        AnsiConsole.MarkupLine("[cyan]=== Environment Discovery ===[/]");

        var table = new Table();
        table.AddColumn("Component");
        table.AddColumn("Status");
        table.AddColumn("Summary");

        var tree = new Tree("[bold cyan]Detailed Inspection[/]");
        var config = new EnvironmentConfig();

        foreach (var component in _components)
        {
            var result = await component.DiscoverAsync(config);

            var statusColor = result.Status switch
            {
                "Found" or "Installed" or "Running" or "GPU Available" => "green",
                "Missing" or "Error" or "No GPU" => "red",
                _ => "yellow"
            };

            table.AddRow(result.Category, $"[{statusColor}]{result.Status}[/]", result.Summary);

            if (result.Details.Any())
            {
                var node = tree.AddNode($"[bold]{result.Category}[/]");
                foreach (var detail in result.Details)
                {
                    node.AddNode($"{detail.Key}: [blue]{detail.Value}[/]");
                }
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold cyan]Recommended Configuration (YAML)[/]");
        AnsiConsole.WriteLine("---");

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        // 1. environment.yaml
        var rootConfig = new RootEnvironmentConfig { Environment = config };
        var envYaml = serializer.Serialize(rootConfig);
        AnsiConsole.WriteLine(envYaml);
        await SaveConfigurationAsync(PathConstants.EnvironmentYaml, envYaml);

        // 2. python-config.yaml
        var toolingConfig = GeneratePythonToolingConfig(config.Repository.Root);
        var toolingYaml = serializer.Serialize(toolingConfig);

        // Save to Aspire-Full.Python project root
        var pythonProjectRoot = Path.Combine(config.Repository.Root, PathConstants.PythonProjectRoot);
        if (Directory.Exists(pythonProjectRoot))
        {
            var pythonConfigPath = Path.Combine(pythonProjectRoot, PathConstants.PythonConfigYaml);
            await File.WriteAllTextAsync(pythonConfigPath, toolingYaml);
            AnsiConsole.MarkupLine($"\n[green]Python configuration saved to: {pythonConfigPath}[/]");
        }
        else
        {
             await SaveConfigurationAsync(PathConstants.PythonConfigYaml, toolingYaml);
        }
    }

    private PythonToolingConfig GeneratePythonToolingConfig(string rootPath)
    {
        // Shared instances for anchors
        var vendorGlobs = new List<string>
        {
            ".vscode", ".vscode/**",
            ".vscode-test", ".vscode-test/**",
            ".vscode-*", ".vscode-*/**",
            "node_modules", "node_modules/**"
        };

        var flake8Ignore = new List<string> { "E203", "W503" };

        var pylintDisable = new List<string> { "missing-docstring", "invalid-name" };

        var pylintVendorRegex = new List<string>
        {
            ".*/\\.vscode/.*", ".*\\\\.vscode\\\\.*",
            ".*/\\.vscode-test/.*", ".*\\\\.vscode-test\\\\.*",
            ".*/\\.vscode-.*", ".*\\\\.vscode-.*"
        };

        // Discover lint roots
        var lintRoots = new List<string>();
        if (Directory.Exists(Path.Combine(rootPath, PathConstants.PythonAgentsDir))) lintRoots.Add(PathConstants.PythonAgentsDir);
        if (Directory.Exists(Path.Combine(rootPath, PathConstants.SandboxesDir))) lintRoots.Add(PathConstants.SandboxesDir);
        if (Directory.Exists(Path.Combine(rootPath, PathConstants.ScriptsDir))) lintRoots.Add(PathConstants.ScriptsDir);
        if (Directory.Exists(Path.Combine(rootPath, PathConstants.ToolsDir))) lintRoots.Add(PathConstants.ToolsDir);

        // Discover test roots
        var testRoots = new List<string>();
        if (Directory.Exists(Path.Combine(rootPath, PathConstants.PythonAgentsTestsDir))) testRoots.Add(PathConstants.PythonAgentsTestsDir);
        if (Directory.Exists(Path.Combine(rootPath, PathConstants.SandboxesDir))) testRoots.Add(PathConstants.SandboxesDir);
        if (Directory.Exists(Path.Combine(rootPath, PathConstants.ScriptsTestsDir))) testRoots.Add(PathConstants.ScriptsTestsDir);

        var pytestAddOpts = new List<string> { "-q" };

        return new PythonToolingConfig
        {
            Lint = new PythonLintConfig
            {
                LineLength = 120,
                VendorGlobs = vendorGlobs,
                Paths = new LintPathsConfig
                {
                    LintRoots = lintRoots,
                    ExcludeGlobs = vendorGlobs // Anchor: *vendor_globs
                },
                Flake8 = new Flake8Config
                {
                    ExtendIgnore = flake8Ignore,
                    Exclude = vendorGlobs // Anchor: *vendor_globs
                },
                PyCodeStyle = new PyCodeStyleConfig
                {
                    Ignore = flake8Ignore // Anchor: *flake8_ignore
                },
                Pylint = new PylintConfig
                {
                    Disable = pylintDisable,
                    Ignore = new List<string> { ".vscode", ".vscode/extensions", ".vscode-test", ".vscode-debug", ".vscode-extensions" },
                    IgnorePaths = pylintVendorRegex,
                    IgnorePatterns = pylintVendorRegex // Anchor: *pylint_vendor_regex
                },
                Pyright = new PyrightConfig
                {
                    Exclude = vendorGlobs // Anchor: *vendor_globs
                },
                Runner = new RunnerConfig
                {
                    AutoTargets = lintRoots, // Anchor: *lint_roots
                    PylintDisable = pylintDisable // Anchor: *pylint_disable
                }
            },
            Test = new PythonTestConfig
            {
                VendorGlobs = vendorGlobs, // Anchor: *vendor_globs
                Paths = new TestPathsConfig
                {
                    TestRoots = testRoots,
                    ExcludeGlobs = vendorGlobs // Anchor: *vendor_globs
                },
                Pytest = new PytestConfig
                {
                    AddOpts = pytestAddOpts,
                    Markers = new List<string> { "slow: marks tests as slow", "gpu: requires NVIDIA GPU" },
                    FilterWarnings = new List<string> { "ignore::DeprecationWarning" }
                },
                Runner = new TestRunnerConfig
                {
                    AutoTargets = testRoots, // Anchor: *test_roots
                    DefaultAddOpts = pytestAddOpts // Anchor: *pytest_addopts
                }
            }
        };
    }

    private async Task SaveConfigurationAsync(string fileName, string yamlContent)
    {
        try
        {
            var root = RepoComponent.LocateRepositoryRoot();
            var configDir = Path.Combine(root, PathConstants.RecommendedConfigDir);
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);

            var configPath = Path.Combine(configDir, fileName);
            await File.WriteAllTextAsync(configPath, yamlContent);
            AnsiConsole.MarkupLine($"\n[green]Configuration declaration saved to: {configPath}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[red]Failed to save configuration declaration: {ex.Message}[/]");
        }
    }
}
