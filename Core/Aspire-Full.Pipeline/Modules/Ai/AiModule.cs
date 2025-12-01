using System.CommandLine;
using Aspire_Full.Pipeline.Constants;
using Aspire_Full.Pipeline.Services;

namespace Aspire_Full.Pipeline.Modules.Ai;

public class AiModule
{
    private readonly AiService _service = new();

    public Command GetCommand()
    {
        var command = new Command(CommandConstants.Ai.Name, CommandConstants.Ai.Description);

        // Provision
        var provisionCommand = new Command(CommandConstants.Ai.Provision, CommandConstants.Ai.ProvisionDesc);
        var registryOption = new Option<string>(["--registry", "-r"], () => "localhost:5000", "Registry URL");
        var imageOption = new Option<string>(["--image", "-i"], () => "aspire-agents", "Image name");
        var tagOption = new Option<string>(["--tag", "-t"], () => "latest", "Image tag");

        provisionCommand.AddOption(registryOption);
        provisionCommand.AddOption(imageOption);
        provisionCommand.AddOption(tagOption);

        provisionCommand.SetHandler(async (registry, image, tag) =>
            await _service.ProvisionAgentsAsync(registry, image, tag),
            registryOption, imageOption, tagOption);
        command.AddCommand(provisionCommand);

        // Models
        var modelsCommand = new Command(CommandConstants.Ai.Models, CommandConstants.Ai.ModelsDesc);
        var listModelsCommand = new Command("list", "List available models");
        listModelsCommand.SetHandler(async () => await _service.ModelsListAsync());

        var viewModelsCommand = new Command("view", "View model details");
        var modelArg = new Argument<string>("model", "Model name");
        viewModelsCommand.AddArgument(modelArg);
        viewModelsCommand.SetHandler(async (model) => await _service.ModelsViewAsync(model), modelArg);

        var runModelsCommand = new Command("run", "Run model (interactive or specific)");
        var runModelArg = new Argument<string?>("model", () => null, "Model name");
        runModelsCommand.AddArgument(runModelArg);
        runModelsCommand.SetHandler(async (model) => await _service.ModelsRunAsync(model), runModelArg);

        modelsCommand.AddCommand(listModelsCommand);
        modelsCommand.AddCommand(viewModelsCommand);
        modelsCommand.AddCommand(runModelsCommand);

        command.AddCommand(modelsCommand);

        // Workflows (Agentic)
        var workflowsCommand = new Command(CommandConstants.Ai.Workflows, CommandConstants.Ai.WorkflowsDesc);

        var wfInitCommand = new Command("init", "Initialize agentic workflows");
        wfInitCommand.SetHandler(async () => await _service.WorkflowsInitAsync());

        var wfNewCommand = new Command("new", "Create new workflow");
        var wfNameArg = new Argument<string>("name", "Workflow name");
        wfNewCommand.AddArgument(wfNameArg);
        wfNewCommand.SetHandler(async (name) => await _service.WorkflowsNewAsync(name), wfNameArg);

        var wfCompileCommand = new Command("compile", "Compile workflows");
        wfCompileCommand.SetHandler(async () => await _service.WorkflowsCompileAsync());

        var wfRunCommand = new Command("run", "Run workflow");
        var wfRunNameArg = new Argument<string>("name", "Workflow name");
        wfRunCommand.AddArgument(wfRunNameArg);
        wfRunCommand.SetHandler(async (name) => await _service.WorkflowsRunAsync(name), wfRunNameArg);

        var wfStatusCommand = new Command("status", "Check workflow status");
        wfStatusCommand.SetHandler(async () => await _service.WorkflowsStatusAsync());

        var wfLogsCommand = new Command("logs", "View workflow logs");
        var wfLogsNameArg = new Argument<string>("name", "Workflow name");
        wfLogsCommand.AddArgument(wfLogsNameArg);
        wfLogsCommand.SetHandler(async (name) => await _service.WorkflowsLogsAsync(name), wfLogsNameArg);

        workflowsCommand.AddCommand(wfInitCommand);
        workflowsCommand.AddCommand(wfNewCommand);
        workflowsCommand.AddCommand(wfCompileCommand);
        workflowsCommand.AddCommand(wfRunCommand);
        workflowsCommand.AddCommand(wfStatusCommand);
        workflowsCommand.AddCommand(wfLogsCommand);

        command.AddCommand(workflowsCommand);

        // Copilot
        var copilotCommand = new Command(CommandConstants.Ai.Copilot, CommandConstants.Ai.CopilotDesc);

        var suggestCommand = new Command("suggest", "Get command suggestions");
        var queryArg = new Argument<string>("query", "Query string");
        suggestCommand.AddArgument(queryArg);
        suggestCommand.SetHandler(async (query) => await _service.CopilotSuggestAsync(query), queryArg);

        var explainCommand = new Command("explain", "Explain a command");
        var explainArg = new Argument<string>("command", "Command to explain");
        explainCommand.AddArgument(explainArg);
        explainCommand.SetHandler(async (cmd) => await _service.CopilotExplainAsync(cmd), explainArg);

        var configCommand = new Command("config", "Configure Copilot");
        configCommand.SetHandler(async () => await _service.CopilotConfigAsync());

        copilotCommand.AddCommand(suggestCommand);
        copilotCommand.AddCommand(explainCommand);
        copilotCommand.AddCommand(configCommand);

        command.AddCommand(copilotCommand);

        return command;
    }
}
