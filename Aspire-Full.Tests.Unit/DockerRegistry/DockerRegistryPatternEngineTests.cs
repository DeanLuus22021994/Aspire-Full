using Aspire_Full.DockerRegistry;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.DockerRegistry.Models;
using Aspire_Full.DockerRegistry.Services;

namespace Aspire_Full.Tests.Unit.DockerRegistry;

public class DockerRegistryPatternEngineTests
{
    [Fact]
    public void FormatRepository_UsesTokens()
    {
        var options = new DockerRegistryPatternOptions
        {
            RepositoryTemplate = "aspire/{service}-{environment}",
            TagTemplate = "{version}-{architecture}",
            Namespace = "aspire",
            DefaultEnvironment = "dev",
            DefaultArchitecture = "linux-x64",
            DefaultVersion = "1.0.0"
        };

        var engine = new DockerRegistryPatternEngine(options);
        var descriptor = new DockerImageDescriptor
        {
            Service = "api",
            Environment = "prod",
            Architecture = "linux-arm64",
            Version = "2.0.0"
        };

        var repository = engine.FormatRepository(descriptor, options);
        var tag = engine.FormatTag(descriptor, options);

        repository.Should().Be("aspire/api-prod");
        tag.Should().Be("2.0.0-linux-arm64");
    }

    [Fact]
    public void TryMatchRepository_ReturnsParsedDescriptor()
    {
        var options = new DockerRegistryPatternOptions
        {
            RepositoryTemplate = "aspire/{service}-{environment}",
            TagTemplate = "{version}-{architecture}",
            Namespace = "aspire"
        };

        var engine = new DockerRegistryPatternEngine(options);
        var success = engine.TryMatchRepository("aspire/frontend-dev", out var descriptor);

        success.Should().BeTrue();
        descriptor.Should().NotBeNull();
        descriptor!.Service.Should().Be("frontend");
        descriptor.Environment.Should().Be("dev");
    }
}
