using System.Diagnostics;

namespace Aspire_Full.Pipeline.Utils;

public static class ProcessUtils
{
    public static async Task<(int ExitCode, string Output)> RunAsync(string command, string arguments, string? workingDirectory = null, bool silent = true, Dictionary<string, string>? envVars = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
        };

        if (envVars != null)
        {
            foreach (var kvp in envVars)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };

        if (!silent)
        {
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.RedirectStandardError = false;
            process.Start();
            await process.WaitForExitAsync();
            return (process.ExitCode, "");
        }

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync(); // Capture error too if needed, or just ignore for now

        await process.WaitForExitAsync();

        var output = await outputTask;
        // We could combine output and error if needed, but for discovery usually stdout is enough.

        return (process.ExitCode, output);
    }

    public static async Task<(int ExitCode, string Output)> RunAsync(string command, string[] args, string? workingDirectory = null, bool silent = true, Dictionary<string, string>? envVars = null)
    {
        var startInfo = new ProcessStartInfo(command)
        {
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        if (envVars != null)
        {
            foreach (var kvp in envVars)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };

        if (!silent)
        {
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.RedirectStandardError = false;
            process.Start();
            await process.WaitForExitAsync();
            return (process.ExitCode, "");
        }

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await outputTask);
    }
}
