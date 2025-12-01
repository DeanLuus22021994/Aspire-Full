using System.CommandLine;
using Aspire_Full.Pipeline.Services;
using Aspire_Full.Pipeline.Constants;

namespace Aspire_Full.Pipeline.Modules.Pipeline;

public class PipelineModule
{
    private readonly PipelineService _service = new();

    public Command GetCommand()
    {
        var command = new Command(CommandConstants.Pipeline.Name, CommandConstants.Pipeline.Description);

        var solutionOption = new Option<string>(["--solution", "-s"], () => PathConstants.SolutionFilter, CommandConstants.Pipeline.SolutionDesc);
        var projectOption = new Option<string>(["--project", "-p"], () => PathConstants.AppHostProject, CommandConstants.Pipeline.ProjectDesc);
        var configOption = new Option<string>(["--configuration", "-c"], () => CliConstants.Release, CommandConstants.Pipeline.ConfigDesc);
        var skipRunOption = new Option<bool>("--skip-run", CommandConstants.Pipeline.SkipRunDesc);
        var verifyOnlyOption = new Option<bool>("--verify-only", CommandConstants.Pipeline.VerifyOnlyDesc);
        var noRestoreOption = new Option<bool>("--no-restore", CommandConstants.Pipeline.NoRestoreDesc);

        command.AddOption(solutionOption);
        command.AddOption(projectOption);
        command.AddOption(configOption);
        command.AddOption(skipRunOption);
        command.AddOption(verifyOnlyOption);
        command.AddOption(noRestoreOption);

        command.SetHandler(async (solution, project, config, skipRun, verifyOnly, noRestore) =>
            await _service.RunPipelineAsync(solution, project, config, skipRun, verifyOnly, noRestore),
            solutionOption, projectOption, configOption, skipRunOption, verifyOnlyOption, noRestoreOption);

        return command;
    }
}
