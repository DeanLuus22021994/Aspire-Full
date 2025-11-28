using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Aspire_Full.WebAssembly.Services;

public sealed class RegistryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly FrontendEnvironmentRegistry _registry;
    private readonly string _environmentKey;

    public RegistryApiClient(HttpClient httpClient, FrontendEnvironmentRegistry registry, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _registry = registry;
        _environmentKey = configuration["FRONTEND_ENVIRONMENT_KEY"] ?? FrontendEnvironmentKeys.DevelopmentDocs;
    }

    public async Task<IReadOnlyList<DockerRegistryRepositoryDto>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var env = _registry.GetByKey(_environmentKey);
            var uri = new Uri(new Uri(env.ApiBaseAddress), "/api/docker-registry/repositories");
            var repositories = await _httpClient.GetFromJsonAsync<List<DockerRegistryRepositoryDto>>(uri, cancellationToken)
                .ConfigureAwait(false);
            return repositories ?? Array.Empty<DockerRegistryRepositoryDto>();
        }
        catch
        {
            return Array.Empty<DockerRegistryRepositoryDto>();
        }
    }
}

public sealed class DockerRegistryRepositoryDto
{
    public string Repository { get; set; } = string.Empty;
    public bool MatchesPattern { get; set; }
    public string? Service { get; set; }
    public string? Environment { get; set; }
    public string? Architecture { get; set; }
}
