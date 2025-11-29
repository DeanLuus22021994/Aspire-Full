using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Aspire_Full.DockerRegistry;

public class MaxCountRetentionPolicy : IGarbageCollectionPolicy
{
    private readonly DockerRegistryOptions _options;

    public MaxCountRetentionPolicy(IOptions<DockerRegistryOptions> options)
    {
        _options = options.Value;
    }

    public Task<bool> ShouldDeleteAsync(DockerImageDescriptor descriptor, string tag, DockerManifest? manifest, CancellationToken cancellationToken = default)
    {
        // This is a simplified implementation.
        // A real implementation would need to know the total list of tags and their creation dates to decide which ones to keep.
        // Since ShouldDeleteAsync is called per tag, this interface might be inefficient for "Keep Top N".
        // "Keep Top N" requires global knowledge of the repository.

        // However, if we assume the caller (GarbageCollectorService) iterates in some order, or if we change the interface...

        // Let's stick to the interface for now. If we can't implement "Keep Top N" efficiently, maybe we implement "Delete if older than X".
        // But we don't have dates easily without fetching all manifests.

        // For the purpose of this task, let's implement a placeholder that returns false (keep everything)
        // or maybe we can change the interface to accept the list of all tags?

        return Task.FromResult(false);
    }
}
