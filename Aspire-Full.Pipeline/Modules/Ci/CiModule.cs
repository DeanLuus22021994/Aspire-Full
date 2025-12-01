using System.CommandLine;
using Aspire_Full.Pipeline.Services;
using Aspire_Full.Pipeline.Constants;

namespace Aspire_Full.Pipeline.Modules.Ci;

public class CiModule
{
    private readonly CiService _service = new();

    public Command GetCommand()
    {
        var command = new Command(CommandConstants.Ci.Name, CommandConstants.Ci.Description);

        var runnerCommand = new Command(CommandConstants.Ci.Runner, CommandConstants.Ci.RunnerDesc);

        // Setup
        var setupCommand = new Command(CommandConstants.Ci.Setup, CommandConstants.Ci.SetupDesc);
        var tokenOption = new Option<string>(["--token", "-t"], "GitHub Personal Access Token");
        var repoOption = new Option<string>(["--repo", "-r"], () => "DeanLuus22021994/Aspire-Full", "GitHub Repository");
        setupCommand.AddOption(tokenOption);
        setupCommand.AddOption(repoOption);
        setupCommand.SetHandler(async (token, repo) => await _service.SetupRunnerAsync(token, repo), tokenOption, repoOption);
        runnerCommand.AddCommand(setupCommand);

        // Start
        var startCommand = new Command(CommandConstants.Ci.Start, CommandConstants.Ci.StartDesc);
        startCommand.SetHandler(async () => await _service.StartRunnerAsync());
        runnerCommand.AddCommand(startCommand);

        // Stop
        var stopCommand = new Command(CommandConstants.Ci.Stop, CommandConstants.Ci.StopDesc);
        stopCommand.SetHandler(async () => await _service.StopRunnerAsync());
        runnerCommand.AddCommand(stopCommand);

        // Status
        var statusCommand = new Command(CommandConstants.Ci.Status, CommandConstants.Ci.StatusDesc);
        statusCommand.SetHandler(async () => await _service.StatusRunnerAsync());
        runnerCommand.AddCommand(statusCommand);

        // Logs
        var logsCommand = new Command(CommandConstants.Ci.Logs, CommandConstants.Ci.LogsDesc);
        var followOption = new Option<bool>(["--follow", "-f"], "Follow logs");
        logsCommand.AddOption(followOption);
        logsCommand.SetHandler(async (follow) => await _service.LogsRunnerAsync(follow), followOption);
        runnerCommand.AddCommand(logsCommand);

        command.AddCommand(runnerCommand);

        // Cache command
        var cacheCommand = new Command(CommandConstants.Ci.Cache, CommandConstants.Ci.CacheDesc);

        var cacheListCommand = new Command(CommandConstants.Ci.List, CommandConstants.Ci.ListDesc);
        cacheListCommand.SetHandler(async () => await _service.CacheListAsync());

        var cacheDeleteCommand = new Command(CommandConstants.Ci.Delete, CommandConstants.Ci.DeleteDesc);
        var keyOption = new Option<string>(["--key", "-k"], "Cache key to delete");
        cacheDeleteCommand.AddOption(keyOption);
        cacheDeleteCommand.SetHandler(async (key) => await _service.CacheDeleteAsync(key), keyOption);

        var cacheClearCommand = new Command(CommandConstants.Ci.Clear, CommandConstants.Ci.ClearDesc);
        cacheClearCommand.SetHandler(async () => await _service.CacheClearAsync());

        cacheCommand.AddCommand(cacheListCommand);
        cacheCommand.AddCommand(cacheDeleteCommand);
        cacheCommand.AddCommand(cacheClearCommand);

        command.AddCommand(cacheCommand);

        // SBOM command
        var sbomCommand = new Command(CommandConstants.Ci.Sbom, CommandConstants.Ci.SbomDesc);
        var outputOption = new Option<string>(["--output", "-o"], () => "sbom.json", "Output file path");
        sbomCommand.AddOption(outputOption);
        sbomCommand.SetHandler(async (output) => await _service.GenerateSbomAsync(output), outputOption);

        command.AddCommand(sbomCommand);

        // Run Local command
        var runLocalCommand = new Command(CommandConstants.Ci.RunLocal, CommandConstants.Ci.RunLocalDesc);
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

        runLocalCommand.SetHandler(async (workflow, job, evt, list, dry, pat, verbose) =>
            await _service.RunLocalAsync(workflow, job, evt, list, dry, pat, verbose),
            workflowOption, jobOption, eventOption, listOption, dryRunOption, patOption, verboseOption);

        command.AddCommand(runLocalCommand);

        return command;
    }
}
