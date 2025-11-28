namespace Aspire_Full.DockerRegistry;

public interface IDockerRegistryClient
{
    Task<IReadOnlyList<DockerRepositoryInfo>> ListRepositoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListTagsAsync(DockerImageDescriptor descriptor, CancellationToken cancellationToken = default);

    Task<DockerManifest?> GetManifestAsync(DockerImageDescriptor descriptor, string tag, CancellationToken cancellationToken = default);

    DockerImageReference BuildReference(DockerImageDescriptor descriptor, string? tag = null);
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
