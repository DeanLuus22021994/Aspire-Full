using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.DockerRegistry;

public sealed class GarbageCollector : IGarbageCollector
{
    private readonly IDockerRegistryClient _client;
    private readonly IEnumerable<IGarbageCollectionPolicy> _policies;
    private readonly DockerRegistryOptions _options;
    private readonly ILogger<GarbageCollector> _logger;

    public GarbageCollector(
        IDockerRegistryClient client,
        IEnumerable<IGarbageCollectionPolicy> policies,
        IOptions<DockerRegistryOptions> options,
        ILogger<GarbageCollector> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task CollectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting garbage collection...");

        try
        {
            var repositories = await _client.ListRepositoriesAsync(cancellationToken);
            foreach (var repoInfo in repositories)
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (repoInfo.Descriptor is null)
                {
                    _logger.LogWarning("Skipping garbage collection for unmatched repository: {Repository}", repoInfo.Name);
                    continue;
                }

                await ProcessRepositoryAsync(repoInfo.Descriptor, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during garbage collection.");
        }

        _logger.LogInformation("Garbage collection completed.");
    }

    private async Task ProcessRepositoryAsync(DockerImageDescriptor descriptor, CancellationToken cancellationToken)
    {
        try
        {
            var tags = await _client.ListTagsAsync(descriptor, cancellationToken);
            _logger.LogInformation("Found {Count} tags for {Repository}", tags.Count, descriptor.Repository);

            foreach (var tag in tags)
            {
                if (cancellationToken.IsCancellationRequested) break;

                DockerManifest? manifest = null;
                try
                {
                    manifest = await _client.GetManifestAsync(descriptor, tag, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch manifest for {Repository}:{Tag}. Skipping.", descriptor.Repository, tag);
                    continue;
                }

                if (manifest == null) continue;

                bool shouldDelete = false;
                foreach (var policy in _policies)
                {
                    if (await policy.ShouldDeleteAsync(descriptor, tag, manifest, cancellationToken))
                    {
                        shouldDelete = true;
                        _logger.LogInformation("Tag {Tag} marked for deletion by policy {PolicyType}", tag, policy.GetType().Name);
                        break;
                    }
                }

                if (shouldDelete)
                {
                    _logger.LogInformation("Deleting {Repository}@{Digest} (Tag: {Tag})", descriptor.Repository, manifest.Digest, tag);
                    await _client.DeleteManifestAsync(descriptor, manifest.Digest, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing repository {Repository}", descriptor.Repository);
        }
    }
}
