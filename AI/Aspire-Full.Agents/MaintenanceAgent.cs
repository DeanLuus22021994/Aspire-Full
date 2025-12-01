using System.Diagnostics;

namespace Aspire_Full.Agents;

public class MaintenanceAgent
{
    public async Task RunAsync(string workspaceRoot, CancellationToken ct = default)
    {
        Console.WriteLine("🚀 Starting Tensor-Optimized Maintenance Agent...");
        Console.WriteLine($"📂 Workspace: {workspaceRoot}");

        // 1. Build the maintenance image
        Console.WriteLine("🔨 Building maintenance image (aspire-maintenance)...");
        var buildArgs = "build -f Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Maintenance -t aspire-maintenance .";
        await RunDockerAsync(buildArgs, workspaceRoot, ct);

        // 2. Run the maintenance container
        // We mount the workspace to /workspace so uv lock updates the local files
        // We pass --gpus all to enable tensor acceleration for compilation
        // We set the working directory to the python-agents folder where pyproject.toml lives
        Console.WriteLine("🏃 Running maintenance task (uv lock --upgrade)...");
        var runArgs = "run --rm --gpus all -v \".:/workspace\" -w /workspace/AI/Aspire-Full.Python/python-agents aspire-maintenance";
        await RunDockerAsync(runArgs, workspaceRoot, ct);

        Console.WriteLine("✅ Maintenance complete. Dependencies are bleeding-edge.");
    }

    private async Task RunDockerAsync(string arguments, string workingDirectory, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"[docker] {e.Data}");
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.Error.WriteLine($"[docker] {e.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new Exception($"Docker command failed with exit code {process.ExitCode}");
        }
    }
}
