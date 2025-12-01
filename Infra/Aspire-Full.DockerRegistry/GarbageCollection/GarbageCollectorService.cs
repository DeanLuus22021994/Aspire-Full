using System;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.DockerRegistry.GarbageCollection;

public class GarbageCollectorService : BackgroundService
{
    private readonly IGarbageCollector _collector;
    private readonly ILogger<GarbageCollectorService> _logger;

    public GarbageCollectorService(
        IGarbageCollector collector,
        ILogger<GarbageCollectorService> logger)
    {
        _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Garbage Collector Service starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _collector.CollectAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during garbage collection cycle.");
            }

            // Run every hour or configured interval
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
