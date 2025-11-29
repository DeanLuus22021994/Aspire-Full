using System.Collections.Generic;
using System.Linq;

namespace Aspire_Full.DockerRegistry.Models;

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
