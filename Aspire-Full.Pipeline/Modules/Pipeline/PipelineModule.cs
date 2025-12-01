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

        var solutionOption = new Option<string>(["--solution", "-s"], () => PathConstants.SolutionFilter, "Solution to build");
        var projectOption = new Option<string>(["--project", "-p"], () => PathConstants.AppHostProject, "AppHost project");
        var configOption = new Option<string>(["--configuration", "-c"], () => "Release", "Build configuration");
        var skipRunOption = new Option<bool>("--skip-run", "Skip the final dotnet run step");
        var verifyOnlyOption = new Option<bool>("--verify-only", "Run format in verify mode");
        var noRestoreOption = new Option<bool>("--no-restore", "Skip restore");

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
