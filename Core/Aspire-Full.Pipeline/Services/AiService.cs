using Aspire_Full.Pipeline.Constants;
using Aspire_Full.Pipeline.Utils;
using Spectre.Console;

namespace Aspire_Full.Pipeline.Services;

public class AiService
{
    public async Task ProvisionAgentsAsync(string registry, string image, string tag)
    {
        var root = GitUtils.GetRepositoryRoot();
        var fullImageName = $"{registry}/{image}:{tag}";

        AnsiConsole.MarkupLine("[cyan]=== Provisioning Aspire Agents ===[/]");

        // Check registry
        var (code, output) = await ProcessUtils.RunAsync(CliConstants.Docker, ["ps", "--filter", $"name={DockerConstants.RegistryContainerName}", "--format", "{{.Names}}"], root, silent: true);
        if (string.IsNullOrWhiteSpace(output))
        {
            AnsiConsole.MarkupLine("[yellow]Registry container not found. Ensure Aspire AppHost is running.[/]");
        }

        AnsiConsole.MarkupLine("[green]Building Agent Image...[/]");
        await ProcessUtils.RunAsync(CliConstants.Docker, ["build", "-t", fullImageName, "-f", PathConstants.AgentDockerfile, "."], root, silent: false);

        AnsiConsole.MarkupLine("[green]Pushing to Internal Registry...[/]");
        await ProcessUtils.RunAsync(CliConstants.Docker, ["push", fullImageName], root, silent: false);

        AnsiConsole.MarkupLine($"[cyan]Provisioning Complete. Image: {fullImageName}[/]");
    }

    public async Task ModelsListAsync()
    {
        await GhUtils.EnsureExtensionAsync("github/gh-models");
        AnsiConsole.MarkupLine("[cyan]Available GitHub Models:[/]");
        await ProcessUtils.RunAsync(CliConstants.Gh, ["models", "list"], silent: false);
    }

    public async Task ModelsViewAsync(string model)
    {
        await GhUtils.EnsureExtensionAsync("github/gh-models");
        await ProcessUtils.RunAsync(CliConstants.Gh, ["models", "view", model], silent: false);
    }

    public async Task ModelsRunAsync(string? model)
    {
        await GhUtils.EnsureExtensionAsync("github/gh-models");
        var args = new List<string> { "models", "run" };
        if (!string.IsNullOrEmpty(model))
            args.Add(model);

        await ProcessUtils.RunAsync(CliConstants.Gh, args.ToArray(), silent: false);
    }

    public async Task WorkflowsInitAsync()
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        AnsiConsole.MarkupLine("[yellow]Initializing agentic workflows...[/]");
        await ProcessUtils.RunAsync(CliConstants.Gh, ["aw", "init"], root, silent: false);
    }

    public async Task WorkflowsNewAsync(string name)
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        AnsiConsole.MarkupLine($"[yellow]Creating workflow: {name}[/]");
        await ProcessUtils.RunAsync(CliConstants.Gh, ["aw", "new", name], root, silent: false);
    }

    public async Task WorkflowsCompileAsync()
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        AnsiConsole.MarkupLine("[yellow]Compiling workflows...[/]");
        await ProcessUtils.RunAsync(CliConstants.Gh, ["aw", "compile"], root, silent: false);
    }

    public async Task WorkflowsRunAsync(string name)
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        AnsiConsole.MarkupLine($"[cyan]Running workflow: {name}[/]");
        await ProcessUtils.RunAsync(CliConstants.Gh, ["aw", "run", name], root, silent: false);
    }

    public async Task WorkflowsStatusAsync()
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        await ProcessUtils.RunAsync(CliConstants.Gh, ["aw", "status"], root, silent: false);
    }

    public async Task WorkflowsLogsAsync(string name)
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        await ProcessUtils.RunAsync(CliConstants.Gh, ["aw", "logs", name], root, silent: false);
    }

    public async Task CopilotSuggestAsync(string query)
    {
        await GhUtils.EnsureExtensionAsync("github/gh-copilot");
        await ProcessUtils.RunAsync(CliConstants.Gh, ["copilot", "suggest", query], silent: false);
    }

    public async Task CopilotExplainAsync(string command)
    {
        await GhUtils.EnsureExtensionAsync("github/gh-copilot");
        await ProcessUtils.RunAsync(CliConstants.Gh, ["copilot", "explain", command], silent: false);
    }

    public async Task CopilotConfigAsync()
    {
        await GhUtils.EnsureExtensionAsync("github/gh-copilot");
        await ProcessUtils.RunAsync(CliConstants.Gh, ["copilot", "config"], silent: false);
    }
}
