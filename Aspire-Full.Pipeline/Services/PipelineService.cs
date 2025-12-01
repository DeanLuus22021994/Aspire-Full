using Spectre.Console;
using Aspire_Full.Pipeline.Utils;
using Aspire_Full.Pipeline.Constants;

namespace Aspire_Full.Pipeline.Services;

public class PipelineService
{
    public async Task RunPipelineAsync(string solution, string project, string config, bool skipRun, bool verifyOnly, bool noRestore)
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
