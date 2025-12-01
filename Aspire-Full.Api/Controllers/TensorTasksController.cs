using System.Linq;
using System.Text.Json;
using Aspire_Full.Tensor;
using Aspire_Full.Tensor.Models;
using Aspire_Full.Tensor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Api.Controllers;

[ApiController]
[Route("api/tensor-tasks")]
public sealed class TensorTasksController : ControllerBase
{
    private static readonly JsonSerializerOptions StreamSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ITensorJobStore _jobStore;
    private readonly ITensorJobCoordinator _coordinator;
    private readonly IReadOnlyList<TensorModelDescriptor> _models;

    public TensorTasksController(
        ITensorJobStore jobStore,
        ITensorJobCoordinator coordinator,
        IOptions<TensorModelCatalogOptions> catalogOptions)
    {
        _jobStore = jobStore;
        _coordinator = coordinator;
        _models = (catalogOptions.Value.Models ?? new List<TensorModelDescriptor>()).ToList();
    }

    [HttpGet("catalog")]
    public ActionResult<IEnumerable<TensorModelSummaryDto>> GetCatalog()
    {
        var summaries = _models.Select(model => new TensorModelSummaryDto
        {
            Id = model.Id,
            DisplayName = model.DisplayName,
            Description = model.Description,
            DocumentationUri = model.DocumentationUri,
            RecommendedExecutionProvider = model.PreferredExecutionProviders.FirstOrDefault()
        }).ToList();

        return Ok(summaries);
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<IEnumerable<TensorJobSummaryDto>>> GetJobs([FromQuery] int limit = 25, CancellationToken cancellationToken = default)
    {
        var jobs = await _jobStore.GetRecentAsync(limit, cancellationToken).ConfigureAwait(false);
        var summaries = jobs.Select(job => new TensorJobSummaryDto
        {
            Id = job.Id,
            ModelId = job.ModelId,
            Status = job.Status,
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt,
            ExecutionProvider = job.ExecutionProvider,
            PromptPreview = job.PromptPreview
        }).ToList();

        return Ok(summaries);
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<ActionResult<TensorJobStatusDto>> GetJob(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _jobStore.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(job);
    }

    [HttpPost("jobs")]
    public async Task<ActionResult<TensorJobStatusDto>> SubmitJob([FromBody] TensorJobSubmissionDto submission, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var job = await _coordinator.SubmitAsync(submission, cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetJob), new { id = job.Id }, job);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Model '{submission.ModelId}' is not available.");
        }
    }

    [HttpGet("jobs/{id:guid}/stream")]
    public async Task StreamJob(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _jobStore.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.ContentType = "application/x-ndjson";
        foreach (var chunk in job.Output.OrderBy(chunk => chunk.Sequence))
        {
            var payload = JsonSerializer.Serialize(chunk, StreamSerializerOptions);
            await Response.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await Response.WriteAsync("\n", cancellationToken).ConfigureAwait(false);
            await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
