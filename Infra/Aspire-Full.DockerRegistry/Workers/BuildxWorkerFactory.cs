using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Abstractions;
using Aspire_Full.DockerRegistry.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.DockerRegistry.Workers;

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

        if (_options.GpuAcceleration.Enabled)
        {
            _logger.LogInformation(
                "GPU acceleration enabled. Bootstrap images: devel={DevelImage}, runtime={RuntimeImage}",
                _options.GpuAcceleration.CudaBootstrapDevelImage,
                _options.GpuAcceleration.CudaBootstrapRuntimeImage);
        }
    }

    public async Task<IBuildxWorker> GetWorkerAsync(CancellationToken cancellationToken = default)
    {
        await _workerSemaphore.WaitAsync(cancellationToken);
        if (_workerPool.TryDequeue(out var worker))
        {
            return worker;
        }

        return new BuildxWorker(Guid.NewGuid().ToString(), _logger, _options.GpuAcceleration);
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

    /// <summary>
    /// Gets the GPU acceleration options for external consumers.
    /// </summary>
    public GpuAccelerationOptions GpuAccelerationOptions => _options.GpuAcceleration;

    public void Dispose()
    {
        _workerSemaphore.Dispose();
        _exporterSemaphore.Dispose();
    }
}
