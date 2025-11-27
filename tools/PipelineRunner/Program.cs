using System.Diagnostics;

Console.Title = "Aspire Pipeline Runner";

var options = PipelineOptions.Parse(args);
var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    Console.WriteLine("\nCancellation requested. Attempting to stop the current step...");
    cancellationSource.Cancel();
    eventArgs.Cancel = true;
};

var steps = BuildSteps(options);
foreach (var step in steps)
{
    Console.WriteLine($"\n==> {step.Description}");
    var exitCode = await RunDotnetAsync(step, options.RepositoryRoot, cancellationSource.Token);
    if (exitCode != 0)
    {
        Console.Error.WriteLine($"Step '{step.Description}' failed with exit code {exitCode}.");
        return exitCode;
    }
}

Console.WriteLine("\nPipeline completed successfully.");
return 0;

static IReadOnlyList<DotnetStep> BuildSteps(PipelineOptions options)
{
    var steps = new List<DotnetStep>
    {
        new("dotnet restore", BuildRestoreArgs(options)),
        new("dotnet clean", ["clean", options.TargetSolutionOrProject]),
        new("dotnet format", BuildFormatArgs(options)),
        new("dotnet build", BuildBuildArgs(options))
    };

    if (!options.SkipRun)
    {
        steps.Add(new("dotnet run", BuildRunArgs(options)));
    }

    return steps;
}

static string[] BuildRestoreArgs(PipelineOptions options)
{
    return ["restore", options.TargetSolutionOrProject];
}

static string[] BuildFormatArgs(PipelineOptions options)
{
    var args = new List<string> { "format", options.TargetSolutionOrProject };
    if (options.VerifyOnly)
    {
        args.Add("--verify-no-changes");
    }

    return args.ToArray();
}

static string[] BuildBuildArgs(PipelineOptions options)
{
    var args = new List<string> { "build", options.TargetSolutionOrProject, "--configuration", options.Configuration };
    if (options.NoRestore)
    {
        args.Add("--no-restore");
    }

    return args.ToArray();
}

static string[] BuildRunArgs(PipelineOptions options)
{
    var args = new List<string>
    {
        "run",
        "--project",
        options.AppHostProject,
        "--configuration",
        options.Configuration,
        "--no-build"
    };

    if (options.RunProfile is { Length: > 0 })
    {
        args.AddRange(["--launch-profile", options.RunProfile]);
    }

    args.AddRange(options.ExtraRunArguments);
    return args.ToArray();
}

static async Task<int> RunDotnetAsync(DotnetStep step, string workingDirectory, CancellationToken token)
{
    var startInfo = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    foreach (var argument in step.Arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
    process.OutputDataReceived += (_, e) =>
    {
        if (!string.IsNullOrWhiteSpace(e.Data))
        {
            Console.WriteLine($"  {e.Data}");
        }
    };

    process.ErrorDataReceived += (_, e) =>
    {
        if (!string.IsNullOrWhiteSpace(e.Data))
        {
            Console.Error.WriteLine($"  {e.Data}");
        }
    };

    if (!process.Start())
    {
        throw new InvalidOperationException($"Failed to start '{step.Description}'.");
    }

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    using var registration = token.Register(() =>
    {
        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // No-op: best effort kill.
            }
        }
    });

    try
    {
        await process.WaitForExitAsync(token);
    }
    catch (OperationCanceledException)
    {
        return -1;
    }

    return process.ExitCode;
}

internal sealed record DotnetStep(string Description, IReadOnlyList<string> Arguments);

internal sealed class PipelineOptions
{
    private PipelineOptions(
        string repositoryRoot,
        string targetSolutionOrProject,
        string appHostProject,
        string configuration,
        bool skipRun,
        bool verifyOnly,
        bool noRestore,
        string? runProfile,
        IReadOnlyList<string> extraRunArguments)
    {
        RepositoryRoot = repositoryRoot;
        TargetSolutionOrProject = targetSolutionOrProject;
        AppHostProject = appHostProject;
        Configuration = configuration;
        SkipRun = skipRun;
        VerifyOnly = verifyOnly;
        NoRestore = noRestore;
        RunProfile = runProfile;
        ExtraRunArguments = extraRunArguments;
    }

    public string RepositoryRoot { get; }
    public string TargetSolutionOrProject { get; }
    public string AppHostProject { get; }
    public string Configuration { get; }
    public bool SkipRun { get; }
    public bool VerifyOnly { get; }
    public bool NoRestore { get; }
    public string? RunProfile { get; }
    public IReadOnlyList<string> ExtraRunArguments { get; }

    public static PipelineOptions Parse(string[] args)
    {
        var repositoryRoot = LocateRepositoryRoot();
        var solutionOrProject = "Aspire-Full.slnf";
        var appProject = "Aspire-Full/Aspire-Full.csproj";
        var configuration = Environment.GetEnvironmentVariable("DOTNET_CONFIGURATION") ?? "Release";
        var runProfile = "headless";
        var skipRun = false;
        var verifyOnly = false;
        var noRestore = false;
        var extraRunArguments = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--solution":
                case "-s":
                    solutionOrProject = RequireNext(args, ref i, "--solution");
                    break;
                case "--project":
                case "-p":
                    appProject = RequireNext(args, ref i, "--project");
                    break;
                case "--configuration":
                case "-c":
                    configuration = RequireNext(args, ref i, "--configuration");
                    break;
                case "--run-profile":
                    runProfile = RequireNext(args, ref i, "--run-profile");
                    break;
                case "--skip-run":
                    skipRun = true;
                    break;
                case "--verify-only":
                    verifyOnly = true;
                    break;
                case "--no-restore":
                    noRestore = true;
                    break;
                case "--run-arg":
                    extraRunArguments.Add(RequireNext(args, ref i, "--run-arg"));
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unrecognized argument '{args[i]}'. Use --help for usage info.");
            }
        }

        EnsurePathExists(repositoryRoot, solutionOrProject, "solution or project");
        EnsurePathExists(repositoryRoot, appProject, "application project");

        return new PipelineOptions(
            repositoryRoot,
            solutionOrProject,
            appProject,
            configuration,
            skipRun,
            verifyOnly,
            noRestore,
            runProfile,
            extraRunArguments);
    }

    private static void EnsurePathExists(string root, string relativePath, string description)
    {
        var fullPath = Path.Combine(root, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Could not locate the {description} '{relativePath}'.", fullPath);
        }
    }

    private static string RequireNext(IReadOnlyList<string> args, ref int index, string name)
    {
        if (index + 1 >= args.Count)
        {
            throw new ArgumentException($"Missing value after {name}.");
        }

        index++;
        return args[index];
    }

    private static string LocateRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(current, "Aspire-Full.slnx")))
        {
            return current;
        }

        var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        if (File.Exists(Path.Combine(candidate, "Aspire-Full.slnx")))
        {
            return candidate;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root. Run from the repo or pass absolute paths.");
    }

    private static void PrintHelp()
    {
        const string helpText = """
Usage: dotnet run --project tools/PipelineRunner [options]

Options:
  -s|--solution <Path>       Solution or solution filter to clean/format/build (default: Aspire-Full.slnx)
  -p|--project <Path>        Application project to execute for dotnet run (default: Aspire-Full/Aspire-Full.csproj)
  -c|--configuration <Name>  Build/run configuration (default: Release)
     --run-profile <Name>    Launch profile for dotnet run (default: headless)
     --run-arg <Value>       Additional argument forwarded to dotnet run (can be repeated)
     --skip-run              Skip the final dotnet run step
     --verify-only           Run dotnet format in verify-only mode
     --no-restore            Pass --no-restore to dotnet build
  -h|--help                  Show this help message
""";

        Console.WriteLine(helpText);
    }
}
