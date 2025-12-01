using System.CommandLine;
using Spectre.Console;
using Aspire_Full.Pipeline.Utils;

namespace Aspire_Full.Pipeline.Modules.Infra;

public class InfraModule
{
    public Command GetCommand()
    {
        var command = new Command("infra", "Manage Docker infrastructure");

        var initCommand = new Command("init", "Initialize Docker volumes and network");
        initCommand.SetHandler(InitInfrastructureAsync);
        command.AddCommand(initCommand);

        return command;
    }

    private async Task InitInfrastructureAsync()
    {
        AnsiConsole.MarkupLine("[cyan]=== Aspire-Full Docker Infrastructure ===[/]");
        AnsiConsole.WriteLine();

        // Check Docker
        if (!await DockerUtils.IsDockerRunningAsync())
        {
            AnsiConsole.MarkupLine("[red]Docker is not running. Please start Docker Desktop first.[/]");
            Environment.Exit(1);
        }

        // Network
        var networkName = "aspire-network";
        AnsiConsole.MarkupLine("[green]Checking Docker network...[/]");
        if (!await DockerUtils.NetworkExistsAsync(networkName))
        {
            await DockerUtils.CreateNetworkAsync(networkName);
            AnsiConsole.MarkupLine($"  [green]Created: {networkName}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"  [gray]Exists: {networkName}[/]");
        }

        // Volumes
        var volumes = new[]
        {
            ("aspire-nuget-cache", "NuGet package cache"),
            ("aspire-dotnet-tools", ".NET global tools"),
            ("aspire-aspire-cli", "Aspire CLI data"),
            ("aspire-vscode-extensions", "VS Code server extensions"),
            ("aspire-workspace", "Workspace source code"),
            ("aspire-dashboard-data", "Aspire Dashboard data"),
            ("aspire-docker-data", "Docker-in-Docker daemon data"),
            ("aspire-docker-certs", "Docker TLS certificates"),
            ("aspire-runner-data", "Runner configuration and state"),
            ("aspire-runner-work", "Runner work directory"),
            ("aspire-runner-nuget", "Runner NuGet cache"),
            ("aspire-runner-npm", "Runner npm cache"),
            ("aspire-runner-toolcache", "GitHub Actions tool cache"),
            ("aspire-github-mcp-data", "GitHub MCP data"),
            ("aspire-github-mcp-logs", "GitHub MCP logs"),
            ("aspire-github-mcp-cache", "GitHub MCP cache"),
            ("aspire-postgres-data", "PostgreSQL database"),
            ("aspire-redis-data", "Redis cache")
        };

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Checking Docker volumes...[/]");

        int created = 0;
        int existing = 0;

        foreach (var (name, desc) in volumes)
        {
            if (!await DockerUtils.VolumeExistsAsync(name))
            {
                await DockerUtils.CreateVolumeAsync(name);
                AnsiConsole.MarkupLine($"  [green]Created: {name}[/]");
                AnsiConsole.MarkupLine($"           [gray]{desc}[/]");
                created++;
            }
            else
            {
                AnsiConsole.MarkupLine($"  [gray]Exists: {name}[/]");
                existing++;
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]=== Summary ===[/]");
        AnsiConsole.MarkupLine($"[green]Volumes created: {created}[/]");
        AnsiConsole.MarkupLine($"[gray]Volumes existing: {existing}[/]");
        AnsiConsole.MarkupLine($"[cyan]Total: {volumes.Length}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Infrastructure ready![/]");
    }
}
