using System;
using System.Collections.Generic;
using Aspire_Full.DockerRegistry.Configuration;

namespace Aspire_Full.DockerRegistry.Models;

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

    /// <summary>
    /// Additional metadata for traceability and rotation (e.g., build ID, timestamp, git commit).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

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
