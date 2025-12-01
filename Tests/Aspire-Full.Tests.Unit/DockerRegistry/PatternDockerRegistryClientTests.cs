using System.Net;
using Aspire_Full.DockerRegistry;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.DockerRegistry.Models;
using Aspire_Full.DockerRegistry.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Tests.Unit.DockerRegistry;

public class PatternDockerRegistryClientTests
{
    [Fact]
    public async Task ListRepositoriesAsync_ParsesCatalogResponse()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.Contains("_catalog", StringComparison.OrdinalIgnoreCase) == true)
            {
                var payload = "{\"repositories\":[\"aspire/api-dev\",\"aspire/web-dev\"]}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = CreateClient(handler);
        var repositories = await client.ListRepositoriesAsync();

        repositories.Should().HaveCount(2);
        repositories[0].Repository.Should().Be("aspire/api-dev");
        repositories[0].MatchesPattern.Should().BeTrue();
        repositories[0].Descriptor.Should().NotBeNull();
        repositories[0].Descriptor!.Service.Should().Be("api");
    }

    [Fact]
    public async Task ListTagsAsync_FallsBackWhenRegistryUnavailable()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("offline"));
        var client = CreateClient(handler);

        var tags = await client.ListTagsAsync(new DockerImageDescriptor { Service = "api" });

        tags.Should().NotBeEmpty();
    }

    private static PatternDockerRegistryClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5000/")
        };

        var options = Options.Create(new DockerRegistryOptions
        {
            BaseAddress = "http://localhost:5000/",
            Patterns = new DockerRegistryPatternOptions
            {
                RepositoryTemplate = "aspire/{service}-{environment}",
                TagTemplate = "{version}-{architecture}",
                Namespace = "aspire",
                DefaultEnvironment = "dev",
                DefaultArchitecture = "linux-x64",
                DefaultVersion = "1.0.0"
            },
            EnableOfflineFallback = true
        });

        return new PatternDockerRegistryClient(httpClient, options, NullLogger<PatternDockerRegistryClient>.Instance);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
