using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.DockerRegistry.Workers;

internal sealed class BuildxWorker : IBuildxWorker
{
    private readonly ILogger _logger;

    public string Id { get; }

    public BuildxWorker(string id, ILogger logger)
    {
        Id = id;
        _logger = logger;
    }

    public async Task ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Worker {WorkerId} executing: {Command}", Id, command);

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"buildx {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("Worker {WorkerId} failed with exit code {ExitCode}. Error: {Error}", Id, process.ExitCode, error);
            throw new DockerRegistryException(DockerRegistryErrorCode.BuildxError, $"Buildx command failed: {error}");
        }

        _logger.LogInformation("Worker {WorkerId} completed successfully. Output: {Output}", Id, output);
    }
}
