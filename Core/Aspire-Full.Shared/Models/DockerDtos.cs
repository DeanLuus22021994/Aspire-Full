namespace Aspire_Full.Shared.Models;

public sealed record DockerRegistryRepository
{
    public required string Repository { get; init; }
    public bool MatchesPattern { get; init; }
    public string? Service { get; init; }
    public string? Environment { get; init; }
    public string? Architecture { get; init; }
}

public sealed record DockerManifest
{
    public required string Repository { get; init; }
    public required string Tag { get; init; }
    public required string Digest { get; init; }
    public required long TotalSize { get; init; }
    public required IList<DockerManifestLayer> Layers { get; init; }
}

public sealed record DockerManifestLayer
{
    public required string MediaType { get; init; }
    public required string Digest { get; init; }
    public required long Size { get; init; }
}
