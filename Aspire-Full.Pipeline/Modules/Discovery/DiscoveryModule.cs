using Aspire_Full.Pipeline.Modules.Discovery.Components;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Aspire_Full.Pipeline.Modules.Discovery;

public class DiscoveryModule
{
    private readonly IDiscoveryComponent[] _components;

    public DiscoveryModule()
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

    public async Task RunAsync()
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

        var yamlContent = serializer.Serialize(config);
        AnsiConsole.WriteLine(yamlContent);

        await SaveConfigurationAsync(yamlContent);
    }

    private async Task SaveConfigurationAsync(string yamlContent)
    {
        try
        {
            var root = RepoComponent.LocateRepositoryRoot();
            var configDir = Path.Combine(root, ".config", "recommended");
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);

            var configPath = Path.Combine(configDir, "environment.yaml");
            await File.WriteAllTextAsync(configPath, yamlContent);
            AnsiConsole.MarkupLine($"\n[green]Configuration declaration saved to: {configPath}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[red]Failed to save configuration declaration: {ex.Message}[/]");
        }
    }
}
