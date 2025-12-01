using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.DockerRegistry.Workers;

internal sealed class BuildxExporter : IBuildxExporter
{
    private readonly ILogger _logger;

    public string Id { get; }

    public BuildxExporter(string id, ILogger logger)
    {
        Id = id;
        _logger = logger;
    }

    public async Task ExportAsync(string artifactId, string destination, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Exporter {ExporterId} exporting {ArtifactId} to {Destination}", Id, artifactId, destination);

        // Example: docker buildx imagetools create -t destination artifactId
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"buildx imagetools create -t {destination} {artifactId}",
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
            _logger.LogError("Exporter {ExporterId} failed with exit code {ExitCode}. Error: {Error}", Id, process.ExitCode, error);
            throw new DockerRegistryException(DockerRegistryErrorCode.BuildxError, $"Buildx export failed: {error}");
        }

        _logger.LogInformation("Exporter {ExporterId} completed successfully. Output: {Output}", Id, output);
    }
}
