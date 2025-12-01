using System.Diagnostics;
using Spectre.Console;
using Aspire_Full.Pipeline.Utils;
using Aspire_Full.Pipeline.Constants;

namespace Aspire_Full.Pipeline.Services;

public class DevService
{
    public async Task StartAspireAsync(bool wait)
    {
        var root = GitUtils.GetRepositoryRoot();
        var pidFile = Path.Combine(root, PathConstants.PidFile);

        if (IsRunning(pidFile))
        {
            AnsiConsole.MarkupLine("[yellow]Aspire is already running.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[cyan]Starting Aspire AppHost...[/]");

        if (wait)
        {
            await ProcessUtils.RunAsync(CliConstants.Dotnet, ["run", "--project", PathConstants.AppHostProjectName, "--no-build", "--launch-profile", "headless", "--configuration", CliConstants.Release], root, silent: false, envVars: EnvConstants.AspireAppHost);
        }
        else
        {
            // Start in background
            var startInfo = new ProcessStartInfo(CliConstants.Dotnet)
            {
                WorkingDirectory = root,
                UseShellExecute = false, // Must be false to set env vars
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(PathConstants.AppHostProjectName);
            startInfo.ArgumentList.Add("--no-build");
            startInfo.ArgumentList.Add("--launch-profile");
            startInfo.ArgumentList.Add("headless");
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add(CliConstants.Release);

            foreach (var kvp in EnvConstants.AspireAppHost)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }

            var process = Process.Start(startInfo);
            if (process != null)
            {
                await File.WriteAllTextAsync(pidFile, process.Id.ToString());
                AnsiConsole.MarkupLine($"[green]Aspire started in background (PID: {process.Id})[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to start Aspire process.[/]");
            }
        }
    }

    public async Task StopAspireAsync()
    {
        var root = GitUtils.GetRepositoryRoot();
        var pidFile = Path.Combine(root, PathConstants.PidFile);

        if (File.Exists(pidFile))
        {
            if (int.TryParse(await File.ReadAllTextAsync(pidFile), out int pid))
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    process.Kill(true); // Kill entire process tree
                    AnsiConsole.MarkupLine($"[yellow]Stopped Aspire process (PID: {pid})[/]");
                }
                catch (ArgumentException)
                {
                    AnsiConsole.MarkupLine("[yellow]Process not running.[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error stopping process: {ex.Message}[/]");
                }
            }
            File.Delete(pidFile);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No PID file found. Aspire might not be running.[/]");
        }
    }

    public async Task StatusAspireAsync()
    {
        var root = GitUtils.GetRepositoryRoot();
        var pidFile = Path.Combine(root, PathConstants.PidFile);

        if (IsRunning(pidFile))
        {
            var pid = await File.ReadAllTextAsync(pidFile);
            AnsiConsole.MarkupLine($"[green]Aspire is RUNNING (PID: {pid})[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Aspire is NOT running.[/]");
        }
    }

    public async Task BuildSolutionAsync()
    {
        var root = GitUtils.GetRepositoryRoot();

        AnsiConsole.MarkupLine("[cyan]Starting GPU-accelerated build...[/]");
        await ProcessUtils.RunAsync("dotnet", ["build", "--configuration", "Release", "--verbosity", "minimal"], root, silent: false, envVars: EnvConstants.BuildAndTest);
    }

    public async Task TestAsync(bool unitOnly, bool e2eOnly, bool aspireOnly, bool coverage, string filter)
    {
        var root = GitUtils.GetRepositoryRoot();

        // Build first
        await BuildSolutionAsync();

        bool runUnit = !e2eOnly && !aspireOnly;
        bool runE2E = !unitOnly && !aspireOnly;
        bool runAspire = !unitOnly && !e2eOnly;

        if (unitOnly) { runE2E = false; runAspire = false; }
        if (e2eOnly) { runUnit = false; runAspire = false; }
        if (aspireOnly) { runUnit = false; runE2E = false; }

        if (runUnit)
        {
            AnsiConsole.MarkupLine("[cyan]Running Unit Tests...[/]");
            var args = new List<string>
            {
                "test", PathConstants.UnitTestProject,
                "--configuration", "Release",
                "--no-build",
                "--logger", "console;verbosity=normal",
                "--results-directory", PathConstants.TestResultsUnit
            };

            if (coverage) args.Add("--collect:XPlat Code Coverage");
            if (!string.IsNullOrEmpty(filter)) { args.Add("--filter"); args.Add(filter); }

            await ProcessUtils.RunAsync("dotnet", args.ToArray(), root, silent: false, envVars: EnvConstants.BuildAndTest);
        }

        if (runE2E)
        {
            AnsiConsole.MarkupLine("[cyan]Running E2E Tests (Dashboard)...[/]");
            var args = new List<string>
            {
                "test", PathConstants.E2ETestProject,
                "--configuration", "Release",
                "--no-build",
                "--logger", "console;verbosity=normal",
                "--results-directory", PathConstants.TestResultsE2E,
                "--filter", string.IsNullOrEmpty(filter) ? "TestCategory=Dashboard" : filter
            };

            await ProcessUtils.RunAsync("dotnet", args.ToArray(), root, silent: false, envVars: EnvConstants.BuildAndTest);
        }

        if (runAspire)
        {
            AnsiConsole.MarkupLine("[cyan]Running Aspire Integration Tests...[/]");
            var args = new List<string>
            {
                "test", PathConstants.E2ETestProject,
                "--configuration", "Release",
                "--no-build",
                "--logger", "console;verbosity=normal",
                "--results-directory", PathConstants.TestResultsAspire,
                "--filter", string.IsNullOrEmpty(filter) ? "TestCategory=AspireIntegration" : filter
            };

            await ProcessUtils.RunAsync("dotnet", args.ToArray(), root, silent: false, envVars: EnvConstants.BuildAndTest);
        }
    }

    public async Task CleanupAsync(bool dryRun, bool force)
    {
        await GhUtils.EnsureExtensionAsync("seachicken/gh-poi");
        var root = GitUtils.GetRepositoryRoot();

        var args = new List<string> { "poi" };
        if (dryRun) args.Add("--dry-run");
        if (force) args.Add("--force");

        AnsiConsole.MarkupLine("[cyan]Cleaning up local branches...[/]");
        await ProcessUtils.RunAsync("gh", args.ToArray(), root, silent: false);
    }

    private bool IsRunning(string pidFile)
    {
        if (!File.Exists(pidFile)) return false;

        if (int.TryParse(File.ReadAllText(pidFile), out int pid))
        {
            try
            {
                Process.GetProcessById(pid);
                return true;
            }
            catch
            {
                // Process not found
                File.Delete(pidFile);
                return false;
            }
        }
        return false;
    }
}
