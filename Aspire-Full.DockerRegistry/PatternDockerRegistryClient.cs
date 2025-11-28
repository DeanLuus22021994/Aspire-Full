using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.DockerRegistry;

public sealed class PatternDockerRegistryClient : IDockerRegistryClient
{
    private const string ManifestMediaType = "application/vnd.docker.distribution.manifest.v2+json";

    private readonly HttpClient _httpClient;
    private readonly DockerRegistryOptions _options;
    private readonly ILogger<PatternDockerRegistryClient> _logger;
    private readonly DockerRegistryPatternEngine _patternEngine;

    public PatternDockerRegistryClient(
        HttpClient httpClient,
        IOptions<DockerRegistryOptions> options,
        ILogger<PatternDockerRegistryClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patternEngine = new DockerRegistryPatternEngine(_options.Patterns);
    }

    public DockerImageReference BuildReference(DockerImageDescriptor descriptor, string? tag = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var resolved = descriptor.WithDefaults(_options.Patterns);
        var repository = _patternEngine.FormatRepository(resolved, _options.Patterns);
        var resolvedTag = tag ?? _patternEngine.FormatTag(resolved, _options.Patterns);
        return new DockerImageReference(repository, resolvedTag);
    }

    public async Task<IReadOnlyList<DockerRepositoryInfo>> ListRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = $"v2/_catalog?n={_options.CatalogPageSize}";
            var response = await _httpClient.GetFromJsonAsync<CatalogResponse>(uri, cancellationToken).ConfigureAwait(false);
            if (response?.Repositories is not { Count: > 0 })
            {
                return _options.EnableOfflineFallback
                    ? BuildSampleRepositories()
                    : Array.Empty<DockerRepositoryInfo>();
            }

            return response.Repositories
                .Select(repo =>
                {
                    var matches = _patternEngine.TryMatchRepository(repo, out var descriptor);
                    return new DockerRepositoryInfo(repo, matches, descriptor);
                })
                .ToList();
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            _logger.LogWarning(ex, "Docker registry catalog request failed. Falling back to pattern data.");
            if (_options.EnableOfflineFallback)
            {
                return BuildSampleRepositories();
            }

            throw new DockerRegistryException("Unable to query Docker registry catalog", ex);
        }
    }

    public async Task<IReadOnlyList<string>> ListTagsAsync(DockerImageDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var reference = BuildReference(descriptor);
        try
        {
            var uri = $"v2/{reference.Repository}/tags/list";
            var response = await _httpClient.GetFromJsonAsync<TagListResponse>(uri, cancellationToken).ConfigureAwait(false);
            return response?.Tags?.Count > 0 ? response.Tags : Array.Empty<string>();
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            _logger.LogWarning(ex, "Docker registry tag listing failed for {Repository}", reference.Repository);
            if (_options.EnableOfflineFallback)
            {
                return GenerateSampleTags(reference);
            }

            throw new DockerRegistryException($"Unable to query tags for {reference.Repository}", ex);
        }
    }

    public async Task<DockerManifest?> GetManifestAsync(DockerImageDescriptor descriptor, string tag, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag cannot be empty", nameof(tag));
        }

        var reference = BuildReference(descriptor, tag);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v2/{reference.Repository}/manifests/{reference.Tag}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestMediaType));

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new DockerRegistryException($"Registry returned {(int)response.StatusCode}: {body}");
            }

            var manifestResponse = await response.Content.ReadFromJsonAsync<ManifestResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (manifestResponse is null)
            {
                return null;
            }

            var digest = response.Headers.TryGetValues("Docker-Content-Digest", out var values)
                ? values.FirstOrDefault() ?? string.Empty
                : string.Empty;

            var layers = manifestResponse.Layers.Select(layer => new DockerManifestLayer
            {
                MediaType = layer.MediaType ?? string.Empty,
                Digest = layer.Digest ?? string.Empty,
                Size = layer.Size
            }).ToList();

            return new DockerManifest
            {
                Repository = reference.Repository,
                Tag = reference.Tag,
                Digest = digest,
                Layers = layers
            };
        }
        catch (Exception ex) when (IsNetworkFailure(ex))
        {
            _logger.LogWarning(ex, "Docker registry manifest request failed for {Reference}", reference.FullyQualified);
            if (_options.EnableOfflineFallback)
            {
                return new DockerManifest
                {
                    Repository = reference.Repository,
                    Tag = reference.Tag,
                    Digest = $"sha256:{Guid.NewGuid():N}",
                    Layers = new List<DockerManifestLayer>
                    {
                        new()
                        {
                            MediaType = ManifestMediaType,
                            Digest = $"sha256:{Guid.NewGuid():N}",
                            Size = 32_768
                        }
                    }
                };
            }

            throw new DockerRegistryException($"Unable to query manifest for {reference.FullyQualified}", ex);
        }
    }

    private IReadOnlyList<DockerRepositoryInfo> BuildSampleRepositories()
    {
        return _options.Patterns.SampleServices.Select(service =>
        {
            var descriptor = new DockerImageDescriptor { Service = service };
            var reference = BuildReference(descriptor);
            return new DockerRepositoryInfo(reference.Repository, true, descriptor.WithDefaults(_options.Patterns));
        }).ToList();
    }

    private static bool IsNetworkFailure(Exception ex)
        => ex is HttpRequestException or TaskCanceledException or OperationCanceledException;

    private IReadOnlyList<string> GenerateSampleTags(DockerImageReference reference)
    {
        var descriptor = _patternEngine.TryMatchRepository(reference.Repository, out var parsed) && parsed is not null
            ? parsed
            : new DockerImageDescriptor { Service = reference.Repository };

        var architectures = new[] { descriptor.Architecture ?? _options.Patterns.DefaultArchitecture, "linux-arm64" };
        var versions = new[] { descriptor.Version ?? _options.Patterns.DefaultVersion, "2.0.0", "nightly" };

        return architectures.SelectMany(arch => versions.Select(version =>
            _patternEngine.FormatTag(descriptor with { Architecture = arch, Version = version }, _options.Patterns)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record CatalogResponse
    {
        [JsonPropertyName("repositories")]
        public List<string> Repositories { get; init; } = new();
    }

    private sealed record TagListResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; init; } = new();
    }

    private sealed record ManifestResponse
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("mediaType")]
        public string? MediaType { get; init; }

        [JsonPropertyName("config")]
        public ManifestLayerResponse? Config { get; init; }

        [JsonPropertyName("layers")]
        public List<ManifestLayerResponse> Layers { get; init; } = new();
    }

    private sealed record ManifestLayerResponse
    {
        [JsonPropertyName("mediaType")]
        public string? MediaType { get; init; }

        [JsonPropertyName("digest")]
        public string? Digest { get; init; }

        [JsonPropertyName("size")]
        public long Size { get; init; }
    }
}
