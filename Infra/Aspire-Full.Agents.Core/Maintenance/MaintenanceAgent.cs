using System.Diagnostics;
using Aspire_Full.Shared;
using Aspire_Full.Shared.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Agents.Core.Maintenance;

/// <summary>
/// Maintenance agent that performs workspace upkeep tasks.
/// Supports GPU-accelerated operations via Docker containers.
/// </summary>
public sealed class MaintenanceAgent : IMaintenanceAgent
{
    private readonly ILogger<MaintenanceAgent>? _logger;
    private readonly TimeProvider _timeProvider;

    public MaintenanceAgent()
        : this(null, TimeProvider.System)
    {
    }

    public MaintenanceAgent(ILogger<MaintenanceAgent>? logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<Result<MaintenanceResult>> RunAsync(string workspaceRoot, CancellationToken ct = default)
    {
        var startTime = _timeProvider.GetTimestamp();
        var executedTasks = new List<string>();

        _logger?.LogInformation("Starting Tensor-Optimized Maintenance Agent in {WorkspaceRoot}", workspaceRoot);

        // 1. Build the maintenance image
        _logger?.LogInformation("Building maintenance image (aspire-maintenance)...");
        var buildArgs = "build -f Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Maintenance -t aspire-maintenance .";
        var buildResult = await RunDockerAsync(buildArgs, workspaceRoot, ct);
        if (!buildResult.IsSuccess)
        {
            return Result<MaintenanceResult>.Failure($"Docker build failed: {buildResult.Error}");
        }
        executedTasks.Add("docker-build");

        // 2. Run the maintenance container with GPU acceleration
        _logger?.LogInformation("Running maintenance task (uv lock --upgrade)...");
        var runArgs = "run --rm --gpus all -v \".:/workspace\" -w /workspace/AI/Aspire-Full.Python/python-agents aspire-maintenance";
        var runResult = await RunDockerAsync(runArgs, workspaceRoot, ct);
        if (!runResult.IsSuccess)
        {
            return Result<MaintenanceResult>.Failure($"Docker run failed: {runResult.Error}");
        }
        executedTasks.Add("uv-lock-upgrade");

        var elapsed = _timeProvider.GetElapsedTime(startTime);
        _logger?.LogInformation("Maintenance complete. Duration: {Duration}", elapsed);

        return Result<MaintenanceResult>.Success(new MaintenanceResult
        {
            ExecutedTasks = executedTasks,
            Duration = elapsed,
            GpuAccelerated = true
        });
    }

    private async Task<Result> RunDockerAsync(string arguments, string workingDirectory, CancellationToken ct)
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
        var errorOutput = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _logger?.LogDebug("[docker] {Output}", e.Data);
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger?.LogWarning("[docker] {Error}", e.Data);
                errorOutput.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                return Result.Failure($"Exit code {process.ExitCode}: {errorOutput}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Process failed: {ex.Message}");
        }
    }
}
