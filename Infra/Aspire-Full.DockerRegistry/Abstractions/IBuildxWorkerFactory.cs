using System.Threading;
using System.Threading.Tasks;

namespace Aspire_Full.DockerRegistry.Abstractions;

public interface IBuildxWorkerFactory
{
    Task<IBuildxWorker> GetWorkerAsync(CancellationToken cancellationToken = default);
    Task ReleaseWorkerAsync(IBuildxWorker worker);

    Task<IBuildxExporter> GetExporterAsync(CancellationToken cancellationToken = default);
    Task ReleaseExporterAsync(IBuildxExporter exporter);
}
