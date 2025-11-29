using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Models;

namespace Aspire_Full.DockerRegistry.Abstractions;

public interface IDockerRegistryClient
{
    Task<IReadOnlyList<DockerRepositoryInfo>> ListRepositoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListTagsAsync(DockerImageDescriptor descriptor, CancellationToken cancellationToken = default);

    Task<DockerManifest?> GetManifestAsync(DockerImageDescriptor descriptor, string tag, CancellationToken cancellationToken = default);

    Task DeleteManifestAsync(DockerImageDescriptor descriptor, string digest, CancellationToken cancellationToken = default);

    DockerImageReference BuildReference(DockerImageDescriptor descriptor, string? tag = null);
}
