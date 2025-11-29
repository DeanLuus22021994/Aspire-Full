using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.DockerRegistry;

public sealed class BuildxWorkerFactory : IBuildxWorkerFactory, IDisposable
{
    private readonly ConcurrentQueue<IBuildxWorker> _workerPool = new();
    private readonly ConcurrentQueue<IBuildxExporter> _exporterPool = new();
    private readonly SemaphoreSlim _workerSemaphore;
    private readonly SemaphoreSlim _exporterSemaphore;
    private readonly DockerRegistryOptions _options;
    private readonly ILogger<BuildxWorkerFactory> _logger;

    public BuildxWorkerFactory(IOptions<DockerRegistryOptions> options, ILogger<BuildxWorkerFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
        _workerSemaphore = new SemaphoreSlim(_options.MaxWorkerPoolSize);
        // Enforce 2 exporters to 1 worker ratio
        _exporterSemaphore = new SemaphoreSlim(_options.MaxWorkerPoolSize * 2);
    }

    public async Task<IBuildxWorker> GetWorkerAsync(CancellationToken cancellationToken = default)
    {
        await _workerSemaphore.WaitAsync(cancellationToken);
        if (_workerPool.TryDequeue(out var worker))
        {
            return worker;
        }

        return new BuildxWorker(Guid.NewGuid().ToString(), _logger);
    }

    public Task ReleaseWorkerAsync(IBuildxWorker worker)
    {
        _workerPool.Enqueue(worker);
        _workerSemaphore.Release();
        return Task.CompletedTask;
    }

    public async Task<IBuildxExporter> GetExporterAsync(CancellationToken cancellationToken = default)
    {
        await _exporterSemaphore.WaitAsync(cancellationToken);
        if (_exporterPool.TryDequeue(out var exporter))
        {
            return exporter;
        }

        return new BuildxExporter(Guid.NewGuid().ToString(), _logger);
    }

    public Task ReleaseExporterAsync(IBuildxExporter exporter)
    {
        _exporterPool.Enqueue(exporter);
        _exporterSemaphore.Release();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _workerSemaphore.Dispose();
        _exporterSemaphore.Dispose();
    }
}

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
        // Simulate execution or use Process.Start
        await Task.Delay(100, cancellationToken);
    }
}

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
        // Simulate export
        await Task.Delay(100, cancellationToken);
    }
}
