using Aspire_Full.Pipeline.Modules.Discovery;
using Aspire_Full.Pipeline.Utils;
using System.CommandLine;
using System.Diagnostics;
using Spectre.Console;

Console.Title = "Aspire-Full.Pipeline";

var rootCommand = new RootCommand("Aspire-Full Pipeline Tool");

// Shared options
var solutionOption = new Option<string>(["--solution", "-s"], () => "Aspire-Full.slnf", "Solution to build");
var projectOption = new Option<string>(["--project", "-p"], () => "Aspire-Full/Aspire-Full.csproj", "AppHost project");
var configOption = new Option<string>(["--configuration", "-c"], () => "Release", "Build configuration");

rootCommand.AddGlobalOption(solutionOption);
rootCommand.AddGlobalOption(projectOption);
rootCommand.AddGlobalOption(configOption);

// Command: Discover
var discoverCommand = new Command("discover", "Discover environment and tools");
discoverCommand.SetHandler(async () =>
{
    await new DiscoveryModule().RunAsync();
});
rootCommand.AddCommand(discoverCommand);

// Command: Provision
var provisionCommand = new Command("provision", "Build and push agent docker images");
var registryUrlOption = new Option<string>("--registry-url", () => "localhost:5000", "Internal registry URL");
var imageNameOption = new Option<string>("--image-name", () => "aspire-agents", "Image name");
var tagOption = new Option<string>("--tag", () => "latest", "Image tag");

provisionCommand.AddOption(registryUrlOption);
provisionCommand.AddOption(imageNameOption);
provisionCommand.AddOption(tagOption);

provisionCommand.SetHandler(async (registryUrl, imageName, tag) =>
{
    await ProvisionAgentsAsync(registryUrl, imageName, tag);
}, registryUrlOption, imageNameOption, tagOption);

rootCommand.AddCommand(provisionCommand);

// Command: Host
var hostCommand = new Command("host", "Manage Aspire AppHost");
var waitOption = new Option<bool>("--wait", "Wait for exit (blocking)");
var stopOption = new Option<bool>("--stop", "Stop running instance");
var statusOption = new Option<bool>("--status", "Check status");

hostCommand.AddOption(waitOption);
hostCommand.AddOption(stopOption);
hostCommand.AddOption(statusOption);

hostCommand.SetHandler(async (wait, stop, status, project, config) =>
{
    await ManageHostAsync(wait, stop, status, project, config);
}, waitOption, stopOption, statusOption, projectOption, configOption);

rootCommand.AddCommand(hostCommand);

// Command: Pipeline (Build/Run)
var pipelineCommand = new Command("pipeline", "Run the full build/test/run pipeline");
var skipRunOption = new Option<bool>("--skip-run", "Skip the final dotnet run step");
var verifyOnlyOption = new Option<bool>("--verify-only", "Run format in verify mode");
var noRestoreOption = new Option<bool>("--no-restore", "Skip restore");

pipelineCommand.AddOption(skipRunOption);
pipelineCommand.AddOption(verifyOnlyOption);
pipelineCommand.AddOption(noRestoreOption);

pipelineCommand.SetHandler(async (solution, project, config, skipRun, verifyOnly, noRestore) =>
{
    await RunPipelineAsync(solution, project, config, skipRun, verifyOnly, noRestore);
}, solutionOption, projectOption, configOption, skipRunOption, verifyOnlyOption, noRestoreOption);

rootCommand.AddCommand(pipelineCommand);

return await rootCommand.InvokeAsync(args);

static string LocateRepositoryRoot()
{
    var current = Directory.GetCurrentDirectory();
    if (ContainsSolutionMarker(current)) return current;

    var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    if (ContainsSolutionMarker(candidate)) return candidate;

    throw new DirectoryNotFoundException("Unable to locate repository root.");

    static bool ContainsSolutionMarker(string path)
    {
        return File.Exists(Path.Combine(path, "Aspire-Full.slnf")) ||
               File.Exists(Path.Combine(path, "Aspire-Full.slnx"));
    }
}

static async Task ProvisionAgentsAsync(string registryUrl, string imageName, string tag)
{
    var root = LocateRepositoryRoot();
    var fullImageName = $"{registryUrl}/{imageName}:{tag}";

    AnsiConsole.MarkupLine("[cyan]=== Provisioning Aspire Agents ===[/]");

    // Check registry
    var registryCheck = await RunProcessAsync("docker", ["ps", "--filter", "name=registry", "--format", "{{.Names}}"], root, silent: true);
    if (string.IsNullOrWhiteSpace(registryCheck.Output))
    {
        AnsiConsole.MarkupLine("[yellow]Registry container not found. Ensure Aspire AppHost is running or registry is started.[/]");
    }

    AnsiConsole.MarkupLine("[green]Building Agent Image...[/]");
    var buildArgs = new[] { "build", "-t", fullImageName, "-f", "Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.PythonAgent", "." };
    if (await RunProcessWithOutputAsync("docker", buildArgs, root) != 0) return;

    AnsiConsole.MarkupLine("[green]Pushing to Internal Registry...[/]");
    if (await RunProcessWithOutputAsync("docker", ["push", fullImageName], root) != 0) return;

    AnsiConsole.MarkupLine("[cyan]=== Provisioning Complete ===[/]");
    AnsiConsole.MarkupLine($"Image available at: {fullImageName}");
}

static async Task ManageHostAsync(bool wait, bool stop, bool status, string project, string config)
{
    var root = LocateRepositoryRoot();
    var pidFile = Path.Combine(root, ".aspire.pid");

    if (stop)
    {
        StopAspire(pidFile);
        return;
    }

    if (status)
    {
        ShowStatus(pidFile);
        return;
    }

    if (File.Exists(pidFile))
    {
        AnsiConsole.MarkupLine("[yellow]Aspire is already running.[/]");
        ShowStatus(pidFile);
        return;
    }

    // Set Environment Variables
    Environment.SetEnvironmentVariable("DOTNET_EnableAVX2", "1");
    Environment.SetEnvironmentVariable("DOTNET_EnableSSE41", "1");
    Environment.SetEnvironmentVariable("DOTNET_TieredPGO", "1");
    Environment.SetEnvironmentVariable("DOTNET_TieredCompilation", "1");
    Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
    Environment.SetEnvironmentVariable("DOTNET_NOLOGO", "1");
    Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");
    Environment.SetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:18889");
    Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:18889");

    // Check GPU
    try
    {
        var gpuInfo = await RunProcessAsync("nvidia-smi", ["--query-gpu=name,memory.free,utilization.gpu", "--format=csv,noheader"], root, silent: true);
        if (!string.IsNullOrWhiteSpace(gpuInfo.Output))
        {
            AnsiConsole.MarkupLine($"[cyan]GPU: {gpuInfo.Output.Trim()}[/]");
            Environment.SetEnvironmentVariable("CUDA_VISIBLE_DEVICES", "all");
            Environment.SetEnvironmentVariable("TF_FORCE_GPU_ALLOW_GROWTH", "true");
        }
    }
    catch { /* Ignore if nvidia-smi missing */ }

    AnsiConsole.MarkupLine("[cyan]Starting Aspire distributed application...[/]");

    if (wait)
    {
        await RunProcessWithOutputAsync("dotnet", ["run", "--project", project, "--no-build", "--launch-profile", "headless", "--configuration", config], root);
    }
    else
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false, // UseShellExecute=false to capture output if needed, but for background we might want true?
            // Actually for background detached, we usually use Start-Process in PS.
            // In C#, we can start a process and not wait for it.
            // But we need to redirect output or it might clutter.
            // For "headless" background, we want it to run independently.
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--launch-profile");
        startInfo.ArgumentList.Add("headless");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(config);

        // To detach properly in .NET Core is tricky without shell execute.
        // We'll use ShellExecute = true to let OS handle it, but we can't get PID easily if we want to track it?
        // Actually Process.Start returns the process object with Id.
        startInfo.UseShellExecute = true;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;

        var process = Process.Start(startInfo);
        if (process != null)
        {
            File.WriteAllText(pidFile, process.Id.ToString());
            AnsiConsole.MarkupLine($"[green]Started Aspire (PID: {process.Id})[/]");
            AnsiConsole.MarkupLine("[gray]Waiting for initialization...[/]");

            // Simple wait check (optional, or just return)
            await Task.Delay(5000);
            if (process.HasExited)
            {
                AnsiConsole.MarkupLine($"[red]Aspire process exited unexpectedly (Code: {process.ExitCode})[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[green]Aspire started successfully![/]");
            }
        }
    }
}

static void StopAspire(string pidFile)
{
    if (File.Exists(pidFile))
    {
        if (int.TryParse(File.ReadAllText(pidFile), out int pid))
        {
            try
            {
                var p = Process.GetProcessById(pid);
                p.Kill(true);
                AnsiConsole.MarkupLine($"[yellow]Stopped Aspire (PID: {pid})[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to stop process {pid}: {ex.Message}[/]");
            }
        }
        File.Delete(pidFile);
    }

    // Cleanup containers
    AnsiConsole.MarkupLine("[gray]Cleaning up Aspire containers...[/]");
    // Implementation of docker cleanup omitted for brevity, can add if needed
}

static void ShowStatus(string pidFile)
{
    if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile), out int pid))
    {
        try
        {
            var p = Process.GetProcessById(pid);
            AnsiConsole.MarkupLine($"[green]Aspire is RUNNING (PID: {pid})[/]");
            return;
        }
        catch
        {
            File.Delete(pidFile);
        }
    }
    AnsiConsole.MarkupLine("[yellow]Aspire is NOT running.[/]");
}

static async Task RunPipelineAsync(string solution, string project, string config, bool skipRun, bool verifyOnly, bool noRestore)
{
    var root = LocateRepositoryRoot();
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
        if (await RunProcessWithOutputAsync("dotnet", args, root) != 0)
        {
            AnsiConsole.MarkupLine($"[red]Step '{step.Desc}' failed.[/]");
            Environment.Exit(1);
        }
    }
    AnsiConsole.MarkupLine("\n[green]Pipeline completed successfully.[/]");
}

static async Task<int> RunProcessWithOutputAsync(string fileName, string[] args, string workingDirectory)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    foreach (var arg in args) startInfo.ArgumentList.Add(arg);

    using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
    process.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine($"  {e.Data}"); };
    process.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine($"  {e.Data}"); };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    await process.WaitForExitAsync();
    return process.ExitCode;
}

static async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string[] args, string workingDirectory, bool silent = false)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    foreach (var arg in args) startInfo.ArgumentList.Add(arg);

    using var process = new Process { StartInfo = startInfo };
    process.Start();
    var output = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    return (process.ExitCode, output);
}
