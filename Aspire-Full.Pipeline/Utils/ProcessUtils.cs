using System.Diagnostics;

namespace Aspire_Full.Pipeline.Utils;

public static class ProcessUtils
{
    public static async Task<(int ExitCode, string Output)> RunAsync(string fileName, string[] args, string workingDirectory = "", bool silent = true)
    {
        if (string.IsNullOrEmpty(workingDirectory))
            workingDirectory = Directory.GetCurrentDirectory();

        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync(); // Capture error too if needed, or just ignore for now

        await process.WaitForExitAsync();

        var output = await outputTask;
        // We could combine output and error if needed, but for discovery usually stdout is enough.

        return (process.ExitCode, output);
    }
}
