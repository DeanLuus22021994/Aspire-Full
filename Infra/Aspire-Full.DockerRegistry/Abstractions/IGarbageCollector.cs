using System.Threading;
using System.Threading.Tasks;

namespace Aspire_Full.DockerRegistry.Abstractions;

public interface IGarbageCollector
{
    Task CollectAsync(CancellationToken cancellationToken = default);
}
