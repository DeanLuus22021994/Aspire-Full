using System.Diagnostics;
using Aspire_Full.DockerRegistry.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Add Registry Server Services (GC, Workers)
builder.Services.AddDockerRegistryServer(builder.Configuration);

builder.Services.AddHostedService<RegistryWorker>();

var host = builder.Build();
host.Run();

public class RegistryWorker : BackgroundService
{
    private readonly ILogger<RegistryWorker> _logger;

    public RegistryWorker(ILogger<RegistryWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Aspire-Full.DockerRegistry Service Started.");
        _logger.LogInformation("Ensuring local registry is running...");

        // In a real scenario, we might check Docker API here.
        // For now, we just log that we are the controller.

        while (!stoppingToken.IsCancellationRequested)
        {
            // Periodic health check or maintenance could go here
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
