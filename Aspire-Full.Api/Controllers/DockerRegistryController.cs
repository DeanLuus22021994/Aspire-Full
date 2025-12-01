using Aspire_Full.DockerRegistry;
using Aspire_Full.DockerRegistry.Abstractions;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using DockerManifestEntity = Aspire_Full.DockerRegistry.Models.DockerManifest;
using DockerImageDescriptor = Aspire_Full.DockerRegistry.Models.DockerImageDescriptor;

namespace Aspire_Full.Api.Controllers;

[ApiController]
[Route("api/docker-registry")]
public class DockerRegistryController : ControllerBase
{
    private readonly IDockerRegistryClient _client;
    private readonly DockerRegistryOptions _options;

    public DockerRegistryController(IDockerRegistryClient client, IOptions<DockerRegistryOptions> options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    [HttpGet("repositories")]
    public async Task<ActionResult<IEnumerable<DockerRegistryRepository>>> GetRepositories(CancellationToken cancellationToken)
    {
        try
        {
            var repositories = await _client.ListRepositoriesAsync(cancellationToken).ConfigureAwait(false);
            var response = repositories.Select(repo => new DockerRegistryRepository
            {
                Repository = repo.Repository,
                MatchesPattern = repo.MatchesPattern,
                Service = repo.Descriptor?.Service,
                Environment = repo.Descriptor?.Environment,
                Architecture = repo.Descriptor?.Architecture
            }).ToList();

            return Ok(response);
        }
        catch (DockerRegistryException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("repositories/{service}/tags")]
    public async Task<ActionResult<IEnumerable<string>>> GetTags(
        string service,
        [FromQuery] string? environment,
        [FromQuery] string? architecture,
        [FromQuery] string? version,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(service))
        {
            return BadRequest("Service is required");
        }

        var descriptor = BuildDescriptor(service, environment, architecture, version);

        try
        {
            var tags = await _client.ListTagsAsync(descriptor, cancellationToken).ConfigureAwait(false);
            return Ok(tags);
        }
        catch (DockerRegistryException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("repositories/{service}/manifests/{tag}")]
    public async Task<ActionResult<DockerManifest>> GetManifest(
        string service,
        string tag,
        [FromQuery] string? environment,
        [FromQuery] string? architecture,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(service) || string.IsNullOrWhiteSpace(tag))
        {
            return BadRequest("Service and tag are required");
        }

        var descriptor = BuildDescriptor(service, environment, architecture, version: null);

        try
        {
            var manifest = await _client.GetManifestAsync(descriptor, tag, cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                return NotFound();
            }

            return Ok(new DockerManifest
            {
                Repository = manifest.Repository,
                Tag = manifest.Tag,
                Digest = manifest.Digest,
                TotalSize = manifest.TotalSize,
                Layers = manifest.Layers.Select(layer => new DockerManifestLayer
                {
                    MediaType = layer.MediaType,
                    Digest = layer.Digest,
                    Size = layer.Size
                }).ToList()
            });
        }
        catch (DockerRegistryException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private DockerImageDescriptor BuildDescriptor(string service, string? environment, string? architecture, string? version)
    {
        var descriptor = new DockerImageDescriptor
        {
            Service = service,
            Environment = environment,
            Architecture = architecture,
            Version = version
        };

        return descriptor.WithDefaults(_options.Patterns);
    }
}
