using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Aspire_Full.Shared;
using Aspire_Full.Shared.Models;

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

    public async Task<IReadOnlyList<DockerRegistryRepository>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var env = _registry.GetByKey(_environmentKey);
            var uri = new Uri(new Uri(env.ApiBaseAddress), "/api/docker-registry/repositories");
            var repositories = await _httpClient.GetFromJsonAsync(uri, AppJsonContext.Default.ListDockerRegistryRepository, cancellationToken)
                .ConfigureAwait(false);
            return repositories ?? new List<DockerRegistryRepository>();
        }
        catch
        {
            return Array.Empty<DockerRegistryRepository>();
        }
    }
}
