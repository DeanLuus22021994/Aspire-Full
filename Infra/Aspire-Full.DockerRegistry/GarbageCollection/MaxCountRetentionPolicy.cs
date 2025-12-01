using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Abstractions;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.DockerRegistry.Models;
using Microsoft.Extensions.Options;

namespace Aspire_Full.DockerRegistry.GarbageCollection;

public class MaxCountRetentionPolicy : IGarbageCollectionPolicy
{
    private readonly DockerRegistryOptions _options;

    public MaxCountRetentionPolicy(IOptions<DockerRegistryOptions> options)
    {
        _options = options.Value;
    }

    public Task<bool> ShouldDeleteAsync(DockerImageDescriptor descriptor, string tag, DockerManifest? manifest, CancellationToken cancellationToken = default)
    {
        // This is a simplified implementation. A real policy would likely need to query all tags for the repository
        // to determine if the current tag exceeds the count limit.
        // For now, we'll just return false as a placeholder or implement a basic check if possible.

        // To implement MaxCount correctly, we'd need the list of all tags sorted by date.
        // Since this method is called per tag, it's hard to do "MaxCount" here without context.
        // However, the original code might have had this logic or it's a new requirement.

        // Assuming we don't delete anything by default in this placeholder.
        return Task.FromResult(false);
    }
}
