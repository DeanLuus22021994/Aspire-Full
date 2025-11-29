namespace Aspire_Full.DockerRegistry;

public interface IDockerRegistryClient
{
    Task<IReadOnlyList<DockerRepositoryInfo>> ListRepositoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListTagsAsync(DockerImageDescriptor descriptor, CancellationToken cancellationToken = default);

    Task<DockerManifest?> GetManifestAsync(DockerImageDescriptor descriptor, string tag, CancellationToken cancellationToken = default);

    Task DeleteManifestAsync(DockerImageDescriptor descriptor, string digest, CancellationToken cancellationToken = default);

    DockerImageReference BuildReference(DockerImageDescriptor descriptor, string? tag = null);
}

public interface IBuildxWorkerFactory
{
    Task<IBuildxWorker> GetWorkerAsync(CancellationToken cancellationToken = default);
    Task ReleaseWorkerAsync(IBuildxWorker worker);

    Task<IBuildxExporter> GetExporterAsync(CancellationToken cancellationToken = default);
    Task ReleaseExporterAsync(IBuildxExporter exporter);
}

public interface IBuildxWorker
{
    string Id { get; }
    Task ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);
}

public interface IBuildxExporter
{
    string Id { get; }
    Task ExportAsync(string artifactId, string destination, CancellationToken cancellationToken = default);
}

public interface IGarbageCollector
{
    Task CollectAsync(CancellationToken cancellationToken = default);
}

public sealed record DockerManifest
{
    public required string Repository { get; init; }
    public required string Tag { get; init; }
    public required string Digest { get; init; }
    public required IReadOnlyList<DockerManifestLayer> Layers { get; init; }
    public long TotalSize => Layers.Sum(layer => layer.Size);
}

public sealed record DockerManifestLayer
{
    public required string MediaType { get; init; }
    public required string Digest { get; init; }
    public required long Size { get; init; }
}
