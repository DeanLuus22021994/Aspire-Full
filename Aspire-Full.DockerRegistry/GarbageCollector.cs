using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.DockerRegistry;

public sealed class GarbageCollector : IGarbageCollector
{
    private readonly IDockerRegistryClient _client;
    private readonly DockerRegistryOptions _options;
    private readonly ILogger<GarbageCollector> _logger;

    public GarbageCollector(IDockerRegistryClient client, IOptions<DockerRegistryOptions> options, ILogger<GarbageCollector> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task CollectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting garbage collection...");
        // Implementation would involve listing repositories, checking tags, and deleting old ones
        // For now, we'll just log
        var repos = await _client.ListRepositoriesAsync(cancellationToken);
        foreach (var repo in repos)
        {
            _logger.LogInformation("Checking repository {Repository}", repo.Repository);
        }
        _logger.LogInformation("Garbage collection completed.");
    }
}
