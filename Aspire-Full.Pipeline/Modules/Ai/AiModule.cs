using System.CommandLine;
using Spectre.Console;
using Aspire_Full.Pipeline.Utils;

namespace Aspire_Full.Pipeline.Modules.Ai;

public class AiModule
{
    public Command GetCommand()
    {
        var command = new Command("ai", "AI and Agent workflows");

        // Provision
        var provisionCommand = new Command("provision", "Provision Aspire Agents");
        var registryOption = new Option<string>(["--registry", "-r"], () => "localhost:5000", "Registry URL");
        var imageOption = new Option<string>(["--image", "-i"], () => "aspire-agents", "Image name");
        var tagOption = new Option<string>(["--tag", "-t"], () => "latest", "Image tag");

        provisionCommand.AddOption(registryOption);
        provisionCommand.AddOption(imageOption);
        provisionCommand.AddOption(tagOption);

        provisionCommand.SetHandler(ProvisionAgentsAsync, registryOption, imageOption, tagOption);
        command.AddCommand(provisionCommand);

        // Models
        var modelsCommand = new Command("models", "Interact with GitHub Models");
        var listModelsCommand = new Command("list", "List available models");
        listModelsCommand.SetHandler(ModelsListAsync);

        var viewModelsCommand = new Command("view", "View model details");
        var modelArg = new Argument<string>("model", "Model name");
        viewModelsCommand.AddArgument(modelArg);
        viewModelsCommand.SetHandler(ModelsViewAsync, modelArg);

        var runModelsCommand = new Command("run", "Run model (interactive or specific)");
        var runModelArg = new Argument<string?>("model", () => null, "Model name");
        runModelsCommand.AddArgument(runModelArg);
        runModelsCommand.SetHandler(ModelsRunAsync, runModelArg);

        modelsCommand.AddCommand(listModelsCommand);
        modelsCommand.AddCommand(viewModelsCommand);
        modelsCommand.AddCommand(runModelsCommand);

        command.AddCommand(modelsCommand);

        // Workflows (Agentic)
        var workflowsCommand = new Command("workflows", "Manage Agentic Workflows");

        var wfInitCommand = new Command("init", "Initialize agentic workflows");
        wfInitCommand.SetHandler(WorkflowsInitAsync);

        var wfNewCommand = new Command("new", "Create new workflow");
        var wfNameArg = new Argument<string>("name", "Workflow name");
        wfNewCommand.AddArgument(wfNameArg);
        wfNewCommand.SetHandler(WorkflowsNewAsync, wfNameArg);

        var wfCompileCommand = new Command("compile", "Compile workflows");
        wfCompileCommand.SetHandler(WorkflowsCompileAsync);

        var wfRunCommand = new Command("run", "Run workflow");
        var wfRunNameArg = new Argument<string>("name", "Workflow name");
        wfRunCommand.AddArgument(wfRunNameArg);
        wfRunCommand.SetHandler(WorkflowsRunAsync, wfRunNameArg);

        var wfStatusCommand = new Command("status", "Check workflow status");
        wfStatusCommand.SetHandler(WorkflowsStatusAsync);

        var wfLogsCommand = new Command("logs", "View workflow logs");
        var wfLogsNameArg = new Argument<string>("name", "Workflow name");
        wfLogsCommand.AddArgument(wfLogsNameArg);
        wfLogsCommand.SetHandler(WorkflowsLogsAsync, wfLogsNameArg);

        workflowsCommand.AddCommand(wfInitCommand);
        workflowsCommand.AddCommand(wfNewCommand);
        workflowsCommand.AddCommand(wfCompileCommand);
        workflowsCommand.AddCommand(wfRunCommand);
        workflowsCommand.AddCommand(wfStatusCommand);
        workflowsCommand.AddCommand(wfLogsCommand);

        command.AddCommand(workflowsCommand);

        // Copilot
        var copilotCommand = new Command("copilot", "GitHub Copilot CLI");

        var suggestCommand = new Command("suggest", "Get command suggestions");
        var queryArg = new Argument<string>("query", "Query string");
        suggestCommand.AddArgument(queryArg);
        suggestCommand.SetHandler(CopilotSuggestAsync, queryArg);

        var explainCommand = new Command("explain", "Explain a command");
        var explainArg = new Argument<string>("command", "Command to explain");
        explainCommand.AddArgument(explainArg);
        explainCommand.SetHandler(CopilotExplainAsync, explainArg);

        var configCommand = new Command("config", "Configure Copilot");
        configCommand.SetHandler(CopilotConfigAsync);

        copilotCommand.AddCommand(suggestCommand);
        copilotCommand.AddCommand(explainCommand);
        copilotCommand.AddCommand(configCommand);

        command.AddCommand(copilotCommand);

        return command;
    }

    private async Task ProvisionAgentsAsync(string registry, string image, string tag)
    {
        var root = GitUtils.GetRepositoryRoot();
        var fullImageName = $"{registry}/{image}:{tag}";

        AnsiConsole.MarkupLine("[cyan]=== Provisioning Aspire Agents ===[/]");

        // Check registry
        var (code, output) = await ProcessUtils.RunAsync("docker", ["ps", "--filter", "name=registry", "--format", "{{.Names}}"], root, silent: true);
        if (string.IsNullOrWhiteSpace(output))
        {
            AnsiConsole.MarkupLine("[yellow]Registry container not found. Ensure Aspire AppHost is running.[/]");
        }

        AnsiConsole.MarkupLine("[green]Building Agent Image...[/]");
        // Assuming Dockerfile is at Aspire-Full.Python/python-agents/Dockerfile.agent relative to root
        var dockerfile = "Aspire-Full.Python/python-agents/Dockerfile.agent";
        await ProcessUtils.RunAsync("docker", ["build", "-t", fullImageName, "-f", dockerfile, "."], root, silent: false);

        AnsiConsole.MarkupLine("[green]Pushing to Internal Registry...[/]");
        await ProcessUtils.RunAsync("docker", ["push", fullImageName], root, silent: false);

        AnsiConsole.MarkupLine($"[cyan]Provisioning Complete. Image: {fullImageName}[/]");
    }

    private async Task ModelsListAsync()
    {
        await GhUtils.EnsureExtensionAsync("github/gh-models");
        AnsiConsole.MarkupLine("[cyan]Available GitHub Models:[/]");
        await ProcessUtils.RunAsync("gh", ["models", "list"], silent: false);
    }

    private async Task ModelsViewAsync(string model)
    {
        await GhUtils.EnsureExtensionAsync("github/gh-models");
        await ProcessUtils.RunAsync("gh", ["models", "view", model], silent: false);
    }

    private async Task ModelsRunAsync(string? model)
    {
        await GhUtils.EnsureExtensionAsync("github/gh-models");
        var args = new List<string> { "models", "run" };
        if (!string.IsNullOrEmpty(model)) args.Add(model);

        await ProcessUtils.RunAsync("gh", args.ToArray(), silent: false);
    }

    private async Task WorkflowsInitAsync()
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        AnsiConsole.MarkupLine("[yellow]Initializing agentic workflows...[/]");
        await ProcessUtils.RunAsync("gh", ["aw", "init"], root, silent: false);
    }

    private async Task WorkflowsNewAsync(string name)
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        AnsiConsole.MarkupLine($"[yellow]Creating workflow: {name}[/]");
        await ProcessUtils.RunAsync("gh", ["aw", "new", name], root, silent: false);
    }

    private async Task WorkflowsCompileAsync()
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        AnsiConsole.MarkupLine("[yellow]Compiling workflows...[/]");
        await ProcessUtils.RunAsync("gh", ["aw", "compile"], root, silent: false);
    }

    private async Task WorkflowsRunAsync(string name)
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        AnsiConsole.MarkupLine($"[cyan]Running workflow: {name}[/]");
        await ProcessUtils.RunAsync("gh", ["aw", "run", name], root, silent: false);
    }

    private async Task WorkflowsStatusAsync()
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        await ProcessUtils.RunAsync("gh", ["aw", "status"], root, silent: false);
    }

    private async Task WorkflowsLogsAsync(string name)
    {
        await GhUtils.EnsureExtensionAsync("githubnext/gh-aw");
        var root = GitUtils.GetRepositoryRoot();
        await ProcessUtils.RunAsync("gh", ["aw", "logs", name], root, silent: false);
    }

    private async Task CopilotSuggestAsync(string query)
    {
        await GhUtils.EnsureExtensionAsync("github/gh-copilot");
        await ProcessUtils.RunAsync("gh", ["copilot", "suggest", query], silent: false);
    }

    private async Task CopilotExplainAsync(string command)
    {
        await GhUtils.EnsureExtensionAsync("github/gh-copilot");
        await ProcessUtils.RunAsync("gh", ["copilot", "explain", command], silent: false);
    }

    private async Task CopilotConfigAsync()
    {
        await GhUtils.EnsureExtensionAsync("github/gh-copilot");
        await ProcessUtils.RunAsync("gh", ["copilot", "config"], silent: false);
    }
}
