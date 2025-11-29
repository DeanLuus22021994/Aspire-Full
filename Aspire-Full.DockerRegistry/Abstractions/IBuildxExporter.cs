using System.Threading;
using System.Threading.Tasks;

namespace Aspire_Full.DockerRegistry.Abstractions;

public interface IBuildxExporter
{
    string Id { get; }
    Task ExportAsync(string artifactId, string destination, CancellationToken cancellationToken = default);
}
