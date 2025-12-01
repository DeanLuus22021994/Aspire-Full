using System.CommandLine;
using Spectre.Console;
using Aspire_Full.Pipeline.Utils;

namespace Aspire_Full.Pipeline.Modules.Pipeline;

public class PipelineModule
{
    public Command GetCommand()
    {
        var command = new Command("pipeline", "Run the full build/test/run pipeline");

        var solutionOption = new Option<string>(["--solution", "-s"], () => "Aspire-Full.slnf", "Solution to build");
        var projectOption = new Option<string>(["--project", "-p"], () => "Aspire-Full/Aspire-Full.csproj", "AppHost project");
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

        command.SetHandler(RunPipelineAsync, solutionOption, projectOption, configOption, skipRunOption, verifyOnlyOption, noRestoreOption);

        return command;
    }

    private async Task RunPipelineAsync(string solution, string project, string config, bool skipRun, bool verifyOnly, bool noRestore)
    {
        var root = GitUtils.GetRepositoryRoot();
        var steps = new List<(string Desc, string[] Args)>
        {
            ("Restore", ["restore", solution]),
            ("Clean", ["clean", solution]),
            ("Format", ["format", solution, verifyOnly ? "--verify-no-changes" : ""]),
            ("Build", ["build", solution, "--configuration", config, noRestore ? "--no-restore" : ""])
        };

        if (!skipRun)
        {
            steps.Add(("Run", ["run", "--project", project, "--configuration", config, "--no-build", "--launch-profile", "headless"]));
        }

        foreach (var step in steps)
        {
            AnsiConsole.MarkupLine($"\n[bold]==> {step.Desc}[/]");
            var args = step.Args.Where(a => !string.IsNullOrEmpty(a)).ToArray();

            // We use ProcessUtils here
            var result = await ProcessUtils.RunAsync("dotnet", args, root, silent: false);
            if (result.ExitCode != 0)
            {
                AnsiConsole.MarkupLine($"[red]Step '{step.Desc}' failed.[/]");
                Environment.Exit(1);
            }
        }
        AnsiConsole.MarkupLine("\n[green]Pipeline completed successfully.[/]");
    }
}
