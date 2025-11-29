using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.DockerRegistry;

public sealed class BuildxWorkerFactory : IBuildxWorkerFactory, IDisposable
{
    private readonly ConcurrentQueue<IBuildxWorker> _workerPool = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly DockerRegistryOptions _options;
    private readonly ILogger<BuildxWorkerFactory> _logger;

    public BuildxWorkerFactory(IOptions<DockerRegistryOptions> options, ILogger<BuildxWorkerFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_options.MaxWorkerPoolSize);
    }

    public async Task<IBuildxWorker> GetWorkerAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        if (_workerPool.TryDequeue(out var worker))
        {
            return worker;
        }

        return new BuildxWorker(Guid.NewGuid().ToString(), _logger);
    }

    public Task ReleaseWorkerAsync(IBuildxWorker worker)
    {
        _workerPool.Enqueue(worker);
        _semaphore.Release();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
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
