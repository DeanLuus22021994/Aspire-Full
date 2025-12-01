using System.CommandLine;
using Spectre.Console;
using Aspire_Full.Pipeline.Utils;

namespace Aspire_Full.Pipeline.Modules.Ci;

public class CiModule
{
    public Command GetCommand()
    {
        var command = new Command("ci", "Manage CI/CD workflows");

        var runnerCommand = new Command("runner", "Manage GitHub Actions runner");

        // Setup
        var setupCommand = new Command("setup", "Setup and start the runner");
        var tokenOption = new Option<string>(["--token", "-t"], "GitHub Personal Access Token");
        var repoOption = new Option<string>(["--repo", "-r"], () => "DeanLuus22021994/Aspire-Full", "GitHub Repository");
        setupCommand.AddOption(tokenOption);
        setupCommand.AddOption(repoOption);
        setupCommand.SetHandler(SetupRunnerAsync, tokenOption, repoOption);
        runnerCommand.AddCommand(setupCommand);

        // Start
        var startCommand = new Command("start", "Start the runner service");
        startCommand.SetHandler(StartRunnerAsync);
        runnerCommand.AddCommand(startCommand);

        // Stop
        var stopCommand = new Command("stop", "Stop the runner service");
        stopCommand.SetHandler(StopRunnerAsync);
        runnerCommand.AddCommand(stopCommand);

        // Status
        var statusCommand = new Command("status", "Check runner status");
        statusCommand.SetHandler(StatusRunnerAsync);
        runnerCommand.AddCommand(statusCommand);

        // Logs
        var logsCommand = new Command("logs", "View runner logs");
        var followOption = new Option<bool>(["--follow", "-f"], "Follow logs");
        logsCommand.AddOption(followOption);
        logsCommand.SetHandler(LogsRunnerAsync, followOption);
        runnerCommand.AddCommand(logsCommand);

        command.AddCommand(runnerCommand);

        // Cache command
        var cacheCommand = new Command("cache", "Manage GitHub Actions cache");

        var cacheListCommand = new Command("list", "List cache entries");
        cacheListCommand.SetHandler(CacheListAsync);

        var cacheDeleteCommand = new Command("delete", "Delete a cache entry");
        var keyOption = new Option<string>(["--key", "-k"], "Cache key to delete");
        cacheDeleteCommand.AddOption(keyOption);
        cacheDeleteCommand.SetHandler(CacheDeleteAsync, keyOption);

        var cacheClearCommand = new Command("clear", "Clear all cache entries");
        cacheClearCommand.SetHandler(CacheClearAsync);

        cacheCommand.AddCommand(cacheListCommand);
        cacheCommand.AddCommand(cacheDeleteCommand);
        cacheCommand.AddCommand(cacheClearCommand);

        command.AddCommand(cacheCommand);

        // SBOM command
        var sbomCommand = new Command("sbom", "Generate SBOM");
        var outputOption = new Option<string>(["--output", "-o"], () => "sbom.json", "Output file path");
        sbomCommand.AddOption(outputOption);
        sbomCommand.SetHandler(GenerateSbomAsync, outputOption);

        command.AddCommand(sbomCommand);

        // Run Local command
        var runLocalCommand = new Command("run-local", "Run GitHub Actions locally using gh-act");
        var workflowOption = new Option<string>(["--workflow", "-w"], "Workflow file to run (e.g. ci.yml)");
        var jobOption = new Option<string>(["--job", "-j"], "Specific job to run");
        var eventOption = new Option<string>(["--event", "-e"], () => "push", "Trigger event");
        var listOption = new Option<bool>(["--list", "-l"], "List available workflows");
        var dryRunOption = new Option<bool>(["--dry-run", "-n"], "Dry run");
        var patOption = new Option<string>(["--pat", "-p"], "GitHub Personal Access Token");
        var verboseOption = new Option<bool>(["--verbose", "-v"], "Verbose output");

        runLocalCommand.AddOption(workflowOption);
        runLocalCommand.AddOption(jobOption);
        runLocalCommand.AddOption(eventOption);
        runLocalCommand.AddOption(listOption);
        runLocalCommand.AddOption(dryRunOption);
        runLocalCommand.AddOption(patOption);
        runLocalCommand.AddOption(verboseOption);

        runLocalCommand.SetHandler(RunLocalAsync, workflowOption, jobOption, eventOption, listOption, dryRunOption, patOption, verboseOption);

        command.AddCommand(runLocalCommand);

        return command;
    }

    private async Task SetupRunnerAsync(string token, string repo)
    {
        if (string.IsNullOrEmpty(token))
        {
            AnsiConsole.MarkupLine("[red]GitHub Token is required. Use --token <token>[/]");
            return;
        }

        AnsiConsole.MarkupLine("[cyan]=== Setting up GitHub Actions Runner ===[/]");

        // Ensure Infra (Network and Volumes)
        await DockerUtils.CreateNetworkAsync("aspire-network");
        await DockerUtils.CreateVolumeAsync("github-runner-data");
        // We should ideally ensure all volumes from InfraModule are created, but for runner this is enough.

        // Create .env file
        var root = GitUtils.GetRepositoryRoot();
        var envFile = Path.Combine(root, ".devcontainer", ".env");
        var content = $@"# GitHub Actions Runner Configuration
# Generated by Aspire-Full.Pipeline on {DateTime.Now:yyyy-MM-dd HH:mm:ss}

GITHUB_TOKEN={token}
GITHUB_REPOSITORY={repo}
RUNNER_NAME=aspire-runner
RUNNER_LABELS=self-hosted,Linux,X64,docker,dotnet,aspire
RUNNER_GROUP=Default
";
        await File.WriteAllTextAsync(envFile, content);
        AnsiConsole.MarkupLine($"[green]Created environment file: {envFile}[/]");

        await StartRunnerAsync();
    }

    private async Task StartRunnerAsync()
    {
        var root = GitUtils.GetRepositoryRoot();
        var composeFile = Path.Combine(root, ".devcontainer", "docker-compose.yml");
        var workDir = Path.GetDirectoryName(composeFile)!;

        AnsiConsole.MarkupLine("[green]Starting runner service...[/]");
        await ProcessUtils.RunAsync("docker", ["compose", "up", "-d", "github-runner"], workDir, silent: false);
    }

    private async Task StopRunnerAsync()
    {
        var root = GitUtils.GetRepositoryRoot();
        var composeFile = Path.Combine(root, ".devcontainer", "docker-compose.yml");
        var workDir = Path.GetDirectoryName(composeFile)!;

        AnsiConsole.MarkupLine("[yellow]Stopping runner service...[/]");
        await ProcessUtils.RunAsync("docker", ["compose", "stop", "github-runner"], workDir, silent: false);
    }

    private async Task StatusRunnerAsync()
    {
        var root = GitUtils.GetRepositoryRoot();
        var composeFile = Path.Combine(root, ".devcontainer", "docker-compose.yml");
        var workDir = Path.GetDirectoryName(composeFile)!;

        await ProcessUtils.RunAsync("docker", ["compose", "ps", "github-runner"], workDir, silent: false);
    }

    private async Task LogsRunnerAsync(bool follow)
    {
        var root = GitUtils.GetRepositoryRoot();
        var composeFile = Path.Combine(root, ".devcontainer", "docker-compose.yml");
        var workDir = Path.GetDirectoryName(composeFile)!;

        var args = new List<string> { "compose", "logs" };
        if (follow) args.Add("-f");
        else args.AddRange(["--tail", "100"]);
        args.Add("github-runner");

        await ProcessUtils.RunAsync("docker", args.ToArray(), workDir, silent: false);
    }

    private async Task EnsureGhExtensionAsync(string extensionName)
    {
        var (code, output) = await ProcessUtils.RunAsync("gh", ["extension", "list"], silent: true);
        if (!output.Contains(extensionName))
        {
            AnsiConsole.MarkupLine($"[yellow]Installing gh extension: {extensionName}...[/]");
            await ProcessUtils.RunAsync("gh", ["extension", "install", extensionName], silent: false);
        }
    }

    private async Task<string> GetGhRepoAsync()
    {
        var (code, output) = await ProcessUtils.RunAsync("gh", ["repo", "view", "--json", "nameWithOwner", "-q", ".nameWithOwner"], silent: true);
        return output.Trim();
    }

    private async Task CacheListAsync()
    {
        await EnsureGhExtensionAsync("actions/gh-actions-cache");
        var repo = await GetGhRepoAsync();
        AnsiConsole.MarkupLine($"[yellow]Listing caches for {repo}...[/]");
        await ProcessUtils.RunAsync("gh", ["actions-cache", "list", "-R", repo], silent: false);
    }

    private async Task CacheDeleteAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            AnsiConsole.MarkupLine("[red]Cache key is required. Use --key <key>[/]");
            return;
        }

        await EnsureGhExtensionAsync("actions/gh-actions-cache");
        var repo = await GetGhRepoAsync();
        AnsiConsole.MarkupLine($"[yellow]Deleting cache {key} from {repo}...[/]");
        await ProcessUtils.RunAsync("gh", ["actions-cache", "delete", key, "-R", repo, "--confirm"], silent: false);
    }

    private async Task CacheClearAsync()
    {
        await EnsureGhExtensionAsync("actions/gh-actions-cache");
        var repo = await GetGhRepoAsync();

        if (!AnsiConsole.Confirm($"Are you sure you want to clear ALL caches for {repo}?")) return;

        AnsiConsole.MarkupLine($"[red]Clearing all caches for {repo}...[/]");

        // List keys first
        var (code, output) = await ProcessUtils.RunAsync("gh", ["actions-cache", "list", "-R", repo, "--json", "key", "-q", ".[].key"], silent: true);
        var keys = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        foreach (var key in keys)
        {
            AnsiConsole.MarkupLine($"Deleting {key}...");
            await ProcessUtils.RunAsync("gh", ["actions-cache", "delete", key, "-R", repo, "--confirm"], silent: false);
        }
        AnsiConsole.MarkupLine("[green]All caches cleared.[/]");
    }

    private async Task GenerateSbomAsync(string output)
    {
        await EnsureGhExtensionAsync("advanced-security/gh-sbom");
        var repo = await GetGhRepoAsync();
        AnsiConsole.MarkupLine($"[cyan]Generating SBOM for {repo}...[/]");

        // gh sbom outputs to stdout, we need to capture it
        var (code, sbomContent) = await ProcessUtils.RunAsync("gh", ["sbom", "-r", repo], silent: true);

        if (code != 0)
        {
            AnsiConsole.MarkupLine("[red]Failed to generate SBOM.[/]");
            return;
        }

        await File.WriteAllTextAsync(output, sbomContent);
        AnsiConsole.MarkupLine($"[green]SBOM generated: {output}[/]");
    }

    private async Task RunLocalAsync(string workflow, string job, string triggerEvent, bool list, bool dryRun, string pat, bool verbose)
    {
        await EnsureGhExtensionAsync("nektos/gh-act");

        // Check Docker
        var (code, _) = await ProcessUtils.RunAsync("docker", ["info"], silent: true);
        if (code != 0)
        {
            AnsiConsole.MarkupLine("[red]Docker is not running. Please start Docker Desktop.[/]");
            return;
        }

        var root = GitUtils.GetRepositoryRoot();

        if (list)
        {
            AnsiConsole.MarkupLine("[yellow]Available workflows:[/]");
            await ProcessUtils.RunAsync("gh", ["act", "-l"], root, silent: false);
            return;
        }

        var args = new List<string> { "act" };

        if (!string.IsNullOrEmpty(triggerEvent)) args.Add(triggerEvent);

        if (!string.IsNullOrEmpty(workflow))
        {
            args.Add("-W");
            args.Add($".github/workflows/{workflow}");
        }

        if (!string.IsNullOrEmpty(job))
        {
            args.Add("-j");
            args.Add(job);
        }

        if (dryRun)
        {
            args.Add("-n");
            AnsiConsole.MarkupLine("[yellow]Dry run mode enabled[/]");
        }

        if (verbose) args.Add("-v");

        if (!string.IsNullOrEmpty(pat))
        {
            args.Add("-s");
            args.Add($"GITHUB_TOKEN={pat}");
            AnsiConsole.MarkupLine("[green]Using provided PAT for authentication[/]");
        }
        else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_TOKEN")))
        {
             // act might pick it up automatically, but explicit is better if we want to be sure
             // But usually act reads env vars.
             // Let's just leave it to act unless user provided one explicitly via arg.
        }

        AnsiConsole.MarkupLine($"[cyan]Running: gh {string.Join(" ", args)}[/]");
        await ProcessUtils.RunAsync("gh", args.ToArray(), root, silent: false);
    }
}
