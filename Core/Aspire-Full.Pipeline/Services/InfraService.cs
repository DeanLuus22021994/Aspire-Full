using Spectre.Console;
using Aspire_Full.Pipeline.Utils;
using Aspire_Full.Pipeline.Constants;

namespace Aspire_Full.Pipeline.Services;

public class InfraService
{
    public async Task InitInfrastructureAsync()
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
        AnsiConsole.MarkupLine("[green]Checking Docker network...[/]");
        if (!await DockerUtils.NetworkExistsAsync(DockerConstants.NetworkName))
        {
            await DockerUtils.CreateNetworkAsync(DockerConstants.NetworkName);
            AnsiConsole.MarkupLine($"  [green]Created: {DockerConstants.NetworkName}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"  [gray]Exists: {DockerConstants.NetworkName}[/]");
        }

        // Volumes
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Checking Docker volumes...[/]");

        int created = 0;
        int existing = 0;

        foreach (var (name, desc) in DockerConstants.Volumes)
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
        AnsiConsole.MarkupLine($"[cyan]Total: {DockerConstants.Volumes.Length}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Infrastructure ready![/]");
    }
}
