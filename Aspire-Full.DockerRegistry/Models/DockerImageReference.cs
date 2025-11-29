namespace Aspire_Full.DockerRegistry.Models;

public sealed record DockerImageReference(string Repository, string Tag)
{
    public string FullyQualified => string.IsNullOrWhiteSpace(Tag) ? Repository : $"{Repository}:{Tag}";
}
