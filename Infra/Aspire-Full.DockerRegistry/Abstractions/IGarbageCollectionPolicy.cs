using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Models;

namespace Aspire_Full.DockerRegistry.Abstractions;

public interface IGarbageCollectionPolicy
{
    Task<bool> ShouldDeleteAsync(DockerImageDescriptor descriptor, string tag, DockerManifest? manifest, CancellationToken cancellationToken = default);
}
