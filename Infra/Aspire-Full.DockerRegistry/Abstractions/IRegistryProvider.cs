using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Models;

namespace Aspire_Full.DockerRegistry.Abstractions;

public interface IRegistryProvider
{
    string Name { get; }
    bool CanHandle(string repository);
    Task<IDockerRegistryClient> CreateClientAsync(CancellationToken cancellationToken = default);
}
