using System.Threading;
using System.Threading.Tasks;

namespace Aspire_Full.DockerRegistry.Abstractions;

public interface IBuildxWorker
{
    string Id { get; }
    Task ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);
}
