using System.CommandLine;
using System.Diagnostics;
using Spectre.Console;
using Aspire_Full.Pipeline.Utils;

namespace Aspire_Full.Pipeline.Modules.Dev;

public class DevModule : IModule
{
    private const string PidFileName = ".aspire.pid";

    public Command GetCommand()
    {
        var command = new Command("dev", "Development workflow operations");

        // Start
        var startCommand = new Command("start", "Start Aspire AppHost");
        var waitOption = new Option<bool>(["--wait", "-w"], "Wait for process to exit (blocking)");
        startCommand.AddOption(waitOption);
        startCommand.SetHandler(StartAspireAsync, waitOption);
        command.AddCommand(startCommand);

        // Stop
        var stopCommand = new Command("stop", "Stop Aspire AppHost");
        stopCommand.SetHandler(StopAspireAsync);
        command.AddCommand(stopCommand);

        // Status
        var statusCommand = new Command("status", "Check Aspire AppHost status");
        statusCommand.SetHandler(StatusAspireAsync);
        command.AddCommand(statusCommand);

        // Build
        var buildCommand = new Command("build", "Build the solution with GPU optimizations");
        buildCommand.SetHandler(BuildSolutionAsync);
        command.AddCommand(buildCommand);

        // Test
        var testCommand = new Command("test", "Run tests");
        var unitOption = new Option<bool>(["--unit", "-u"], "Run unit tests only");
        var e2eOption = new Option<bool>(["--e2e", "-e"], "Run E2E tests only");
        var aspireOption = new Option<bool>(["--aspire", "-a"], "Run Aspire integration tests only");
        var coverageOption = new Option<bool>(["--coverage", "-c"], "Collect code coverage");
        var filterOption = new Option<string>(["--filter", "-f"], "Test filter expression");

        testCommand.AddOption(unitOption);
        testCommand.AddOption(e2eOption);
        testCommand.AddOption(aspireOption);
        testCommand.AddOption(coverageOption);
        testCommand.AddOption(filterOption);

        testCommand.SetHandler(TestAsync, unitOption, e2eOption, aspireOption, coverageOption, filterOption);
        command.AddCommand(testCommand);

        // Cleanup
        var cleanupCommand = new Command("cleanup", "Clean up local branches");
        var dryRunOption = new Option<bool>(["--dry-run", "-n"], "Dry run");
        var forceOption = new Option<bool>(["--force", "-f"], "Force delete");

        cleanupCommand.AddOption(dryRunOption);
        cleanupCommand.AddOption(forceOption);

        cleanupCommand.SetHandler(CleanupAsync, dryRunOption, forceOption);
        command.AddCommand(cleanupCommand);

        return command;
    }

    private async Task StartAspireAsync(bool wait)
    {
        var root = GitUtils.GetRepositoryRoot();
        var pidFile = Path.Combine(root, PidFileName);

        if (IsRunning(pidFile))
        {
            AnsiConsole.MarkupLine("[yellow]Aspire is already running.[/]");
            return;
        }

        // Set environment variables
        var envVars = new Dictionary<string, string>
        {
            ["DOTNET_EnableAVX2"] = "1",
            ["DOTNET_EnableSSE41"] = "1",
            ["DOTNET_TieredPGO"] = "1",
            ["DOTNET_TieredCompilation"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["CUDA_VISIBLE_DEVICES"] = "all",
            ["TF_FORCE_GPU_ALLOW_GROWTH"] = "true",
            ["NVIDIA_VISIBLE_DEVICES"] = "all",
            ["NVIDIA_DRIVER_CAPABILITIES"] = "compute,utility",
            ["NVIDIA_REQUIRE_CUDA"] = "cuda>=12.4,driver>=535",
            ["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] = "true",
            ["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "http://localhost:18889",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:18889"
        };

        AnsiConsole.MarkupLine("[cyan]Starting Aspire AppHost...[/]");

        if (wait)
        {
            await ProcessUtils.RunAsync("dotnet", ["run", "--project", "Aspire-Full", "--no-build", "--launch-profile", "headless", "--configuration", "Release"], root, silent: false, envVars: envVars);
        }
        else
        {
            // Start in background
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = root,
                UseShellExecute = false, // Must be false to set env vars
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add("Aspire-Full");
            startInfo.ArgumentList.Add("--no-build");
            startInfo.ArgumentList.Add("--launch-profile");
            startInfo.ArgumentList.Add("headless");
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add("Release");

            foreach (var kvp in envVars)
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

    private async Task StopAspireAsync()
    {
        var root = GitUtils.GetRepositoryRoot();
        var pidFile = Path.Combine(root, PidFileName);

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

    private async Task StatusAspireAsync()
    {
        var root = GitUtils.GetRepositoryRoot();
        var pidFile = Path.Combine(root, PidFileName);

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

    private async Task BuildSolutionAsync()
    {
        var root = GitUtils.GetRepositoryRoot();

        // Set environment variables
        var envVars = new Dictionary<string, string>
        {
            ["DOTNET_EnableAVX2"] = "1",
            ["DOTNET_EnableSSE41"] = "1",
            ["DOTNET_TieredPGO"] = "1",
            ["DOTNET_ReadyToRun"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["CUDA_VISIBLE_DEVICES"] = "all",
            ["NVIDIA_VISIBLE_DEVICES"] = "all",
            ["NVIDIA_DRIVER_CAPABILITIES"] = "compute,utility",
            ["NVIDIA_REQUIRE_CUDA"] = "cuda>=12.4,driver>=535",
            ["TF_FORCE_GPU_ALLOW_GROWTH"] = "true"
        };

        AnsiConsole.MarkupLine("[cyan]Starting GPU-accelerated build...[/]");
        await ProcessUtils.RunAsync("dotnet", ["build", "--configuration", "Release", "--verbosity", "minimal"], root, silent: false, envVars: envVars);
    }

    private async Task TestAsync(bool unitOnly, bool e2eOnly, bool aspireOnly, bool coverage, string filter)
    {
        var root = GitUtils.GetRepositoryRoot();

        // Build first
        await BuildSolutionAsync();

        var envVars = new Dictionary<string, string>
        {
            ["DOTNET_EnableAVX2"] = "1",
            ["DOTNET_EnableSSE41"] = "1",
            ["DOTNET_TieredPGO"] = "1",
            ["DOTNET_ReadyToRun"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["CUDA_VISIBLE_DEVICES"] = "all",
            ["NVIDIA_VISIBLE_DEVICES"] = "all",
            ["NVIDIA_DRIVER_CAPABILITIES"] = "compute,utility",
            ["NVIDIA_REQUIRE_CUDA"] = "cuda>=12.4,driver>=535",
            ["TF_FORCE_GPU_ALLOW_GROWTH"] = "true"
        };

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
                "test", "Aspire-Full.Tests.Unit",
                "--configuration", "Release",
                "--no-build",
                "--logger", "console;verbosity=normal",
                "--results-directory", "./TestResults/Unit"
            };

            if (coverage) args.Add("--collect:XPlat Code Coverage");
            if (!string.IsNullOrEmpty(filter)) { args.Add("--filter"); args.Add(filter); }

            await ProcessUtils.RunAsync("dotnet", args.ToArray(), root, silent: false, envVars: envVars);
        }

        if (runE2E)
        {
            AnsiConsole.MarkupLine("[cyan]Running E2E Tests (Dashboard)...[/]");
            var args = new List<string>
            {
                "test", "Aspire-Full.Tests.E2E",
                "--configuration", "Release",
                "--no-build",
                "--logger", "console;verbosity=normal",
                "--results-directory", "./TestResults/E2E",
                "--filter", string.IsNullOrEmpty(filter) ? "TestCategory=Dashboard" : filter
            };

            await ProcessUtils.RunAsync("dotnet", args.ToArray(), root, silent: false, envVars: envVars);
        }

        if (runAspire)
        {
            AnsiConsole.MarkupLine("[cyan]Running Aspire Integration Tests...[/]");
            var args = new List<string>
            {
                "test", "Aspire-Full.Tests.E2E",
                "--configuration", "Release",
                "--no-build",
                "--logger", "console;verbosity=normal",
                "--results-directory", "./TestResults/Aspire",
                "--filter", string.IsNullOrEmpty(filter) ? "TestCategory=AspireIntegration" : filter
            };

            await ProcessUtils.RunAsync("dotnet", args.ToArray(), root, silent: false, envVars: envVars);
        }
    }

    private async Task CleanupAsync(bool dryRun, bool force)
    {
        await EnsureGhExtensionAsync("seachicken/gh-poi");
        var root = GitUtils.GetRepositoryRoot();

        var args = new List<string> { "poi" };
        if (dryRun) args.Add("--dry-run");
        if (force) args.Add("--force");

        AnsiConsole.MarkupLine("[cyan]Cleaning up local branches...[/]");
        await ProcessUtils.RunAsync("gh", args.ToArray(), root, silent: false);
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
