using System.ComponentModel.DataAnnotations;

namespace Aspire_Full.DockerRegistry;

/// <summary>
/// Configuration options for interacting with a Docker registry using pattern-based repositories and tags.
/// </summary>
public sealed class DockerRegistryOptions
{
    /// <summary>
    /// Registry base address. Supports HTTP or HTTPS. Defaults to a local development registry.
    /// </summary>
    [Required]
    public string BaseAddress { get; set; } = "http://localhost:5000/";

    /// <summary>
    /// Maximum number of repositories to request per catalog page.
    /// </summary>
    [Range(1, 1000)]
    public int CatalogPageSize { get; set; } = 100;

    /// <summary>
    /// Timeout for outgoing HTTP requests.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Whether to allow untrusted SSL certificates (development only).
    /// </summary>
    public bool AllowInsecureTls { get; set; }

    /// <summary>
    /// Whether to fall back to pattern-derived sample data when the registry cannot be reached.
    /// </summary>
    public bool EnableOfflineFallback { get; set; } = true;

    /// <summary>
    /// Credential configuration for the registry.
    /// </summary>
    public DockerRegistryCredentialOptions Credentials { get; set; } = new();

    /// <summary>
    /// Pattern configuration for repositories and tags.
    /// </summary>
    public DockerRegistryPatternOptions Patterns { get; set; } = new();

    /// <summary>
    /// Maximum number of concurrent buildx workers.
    /// </summary>
    public int MaxWorkerPoolSize { get; set; } = 5;

    /// <summary>
    /// Interval for garbage collection.
    /// </summary>
    public TimeSpan GarbageCollectionInterval { get; set; } = TimeSpan.FromHours(24);
}

public sealed class DockerRegistryCredentialOptions
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? BearerToken { get; set; }

    public bool HasCredentials => !string.IsNullOrWhiteSpace(BearerToken) || !string.IsNullOrWhiteSpace(Username);
}

public sealed class DockerRegistryPatternOptions
{
    /// <summary>
    /// Template for repository names. Supported tokens: namespace, service, environment, architecture, variant.
    /// </summary>
    [Required]
    public string RepositoryTemplate { get; set; } = "{namespace}/{service}-{environment}";

    /// <summary>
    /// Template for tags. Supported tokens: version, environment, architecture, variant.
    /// </summary>
    [Required]
    public string TagTemplate { get; set; } = "{version}-{architecture}";

    /// <summary>
    /// Default namespace used when formatting repositories.
    /// </summary>
    [Required]
    public string Namespace { get; set; } = "aspire";

    public string DefaultEnvironment { get; set; } = "dev";

    public string DefaultArchitecture { get; set; } = "linux-x64";

    public string DefaultVersion { get; set; } = "1.0.0";

    public string? DefaultVariant { get; set; }

    /// <summary>
    /// A list of services used when sample repositories need to be generated offline.
    /// </summary>
    public IList<string> SampleServices { get; set; } = new List<string> { "api", "workers", "frontend" };
}
