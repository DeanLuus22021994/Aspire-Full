namespace Aspire_Full.DockerRegistry;

/// <summary>
/// Represents the logical parts of a container image used for template formatting.
/// </summary>
public sealed record DockerImageDescriptor
{
    public required string Service { get; init; }
    public string? Environment { get; init; }
    public string? Architecture { get; init; }
    public string? Version { get; init; }
    public string? Variant { get; init; }

    public DockerImageDescriptor WithDefaults(DockerRegistryPatternOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return this with
        {
            Environment = string.IsNullOrWhiteSpace(Environment) ? options.DefaultEnvironment : Environment,
            Architecture = string.IsNullOrWhiteSpace(Architecture) ? options.DefaultArchitecture : Architecture,
            Version = string.IsNullOrWhiteSpace(Version) ? options.DefaultVersion : Version,
            Variant = string.IsNullOrWhiteSpace(Variant) ? options.DefaultVariant : Variant
        };
    }
}

public sealed record DockerImageReference(string Repository, string Tag)
{
    public string FullyQualified => string.IsNullOrWhiteSpace(Tag) ? Repository : $"{Repository}:{Tag}";
}

public sealed record DockerRepositoryInfo(string Repository, bool MatchesPattern, DockerImageDescriptor? Descriptor);
