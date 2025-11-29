using System.Threading;
using System.Threading.Tasks;

namespace Aspire_Full.DockerRegistry;

public interface IGarbageCollectionPolicy
{
    Task<bool> ShouldDeleteAsync(DockerImageDescriptor descriptor, string tag, DockerManifest? manifest, CancellationToken cancellationToken = default);
}
