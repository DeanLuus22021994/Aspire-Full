using System.CommandLine;
using Aspire_Full.Pipeline.Services;
using Aspire_Full.Pipeline.Constants;

namespace Aspire_Full.Pipeline.Modules.Dev;

public class DevModule
{
    private readonly DevService _service = new();

    public Command GetCommand()
    {
        var command = new Command(CommandConstants.Dev.Name, CommandConstants.Dev.Description);

        // Start
        var startCommand = new Command(CommandConstants.Dev.Start, "Start Aspire AppHost");
        var waitOption = new Option<bool>(["--wait", "-w"], "Wait for process to exit (blocking)");
        startCommand.AddOption(waitOption);
        startCommand.SetHandler(async (wait) => await _service.StartAspireAsync(wait), waitOption);
        command.AddCommand(startCommand);

        // Stop
        var stopCommand = new Command(CommandConstants.Dev.Stop, "Stop Aspire AppHost");
        stopCommand.SetHandler(async () => await _service.StopAspireAsync());
        command.AddCommand(stopCommand);

        // Status
        var statusCommand = new Command(CommandConstants.Dev.Status, "Check Aspire AppHost status");
        statusCommand.SetHandler(async () => await _service.StatusAspireAsync());
        command.AddCommand(statusCommand);

        // Build
        var buildCommand = new Command(CommandConstants.Dev.Build, "Build the solution with GPU optimizations");
        buildCommand.SetHandler(async () => await _service.BuildSolutionAsync());
        command.AddCommand(buildCommand);

        // Test
        var testCommand = new Command(CommandConstants.Dev.Test, "Run tests");
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

        testCommand.SetHandler(async (unit, e2e, aspire, coverage, filter) =>
            await _service.TestAsync(unit, e2e, aspire, coverage, filter),
            unitOption, e2eOption, aspireOption, coverageOption, filterOption);
        command.AddCommand(testCommand);

        // Cleanup
        var cleanupCommand = new Command(CommandConstants.Dev.Cleanup, "Clean up local branches");
        var dryRunOption = new Option<bool>(["--dry-run", "-n"], "Dry run");
        var forceOption = new Option<bool>(["--force", "-f"], "Force delete");

        cleanupCommand.AddOption(dryRunOption);
        cleanupCommand.AddOption(forceOption);

        cleanupCommand.SetHandler(async (dry, force) => await _service.CleanupAsync(dry, force), dryRunOption, forceOption);
        command.AddCommand(cleanupCommand);

        return command;
    }
}
