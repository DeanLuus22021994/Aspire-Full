using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspire_Full.Connectors;
using Aspire_Full.Tensor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Api.Tensor;

public interface ITensorJobCoordinator
{
    Task<TensorJobStatusDto> SubmitAsync(TensorJobSubmissionDto submission, CancellationToken cancellationToken = default);
}

public interface ITensorVectorBridge
{
    Task<string?> TryPersistAsync(TensorJobStatusDto job, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken);
}

public sealed class TensorVectorBridge : ITensorVectorBridge
{
    private readonly IVectorStoreConnector _vectorConnector;
    private readonly ILogger<TensorVectorBridge> _logger;

    public TensorVectorBridge(IVectorStoreConnector vectorConnector, ILogger<TensorVectorBridge> logger)
    {
        _vectorConnector = vectorConnector;
        _logger = logger;
    }

    public async Task<string?> TryPersistAsync(TensorJobStatusDto job, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken)
    {
        using var activity = TensorDiagnostics.ActivitySource.StartActivity("TensorVectorBridge.PersistEmbedding");
        activity?.SetTag("tensor.job_id", job.Id);
        activity?.SetTag("tensor.vector.length", embedding.Length);

        try
        {
            var request = new VectorStoreConnectorRequest(
                job.Id.ToString(),
                job.Prompt,
                embedding,
                new Dictionary<string, object?>
                {
                    ["model_id"] = job.ModelId,
                    ["execution_provider"] = job.ExecutionProvider ?? "wasm-cpu",
                    ["created_at"] = job.CreatedAt.ToString("O")
                });

            var result = await _vectorConnector.UpsertAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Error ?? "vector_persist_failed");
                _logger.LogWarning("Vector connector failed to persist job {JobId}: {Error}", job.Id, result.Error);
                return null;
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            return result.DocumentId;
        }
        catch (Exception ex)
        {
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                ["exception.type"] = ex.GetType().FullName ?? "Exception",
                ["exception.message"] = ex.Message
            }));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Failed to persist tensor embedding for job {JobId}", job.Id);
            return null;
        }
    }
}

public sealed class TensorJobCoordinator : ITensorJobCoordinator
{
    private readonly ITensorJobStore _store;
    private readonly ITensorVectorBridge _vectorBridge;
    private readonly ILogger<TensorJobCoordinator> _logger;
    private readonly IReadOnlyDictionary<string, TensorModelDescriptor> _catalog;

    public TensorJobCoordinator(
        ITensorJobStore store,
        ITensorVectorBridge vectorBridge,
        IOptions<TensorModelCatalogOptions> catalogOptions,
        ILogger<TensorJobCoordinator> logger)
    {
        _store = store;
        _vectorBridge = vectorBridge;
        _logger = logger;
        var descriptorList = catalogOptions.Value.Models ?? new List<TensorModelDescriptor>();
        _catalog = descriptorList.ToDictionary(model => model.Id, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<TensorJobStatusDto> SubmitAsync(TensorJobSubmissionDto submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        using var activity = TensorDiagnostics.ActivitySource.StartActivity("TensorJobCoordinator.Submit");
        activity?.SetTag("tensor.model_id", submission.ModelId);
        activity?.SetTag("tensor.persist", submission.PersistToVectorStore);

        if (!_catalog.TryGetValue(submission.ModelId, out var model))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "model_not_found");
            throw new KeyNotFoundException($"Model '{submission.ModelId}' was not found in the tensor catalog.");
        }

        var now = DateTimeOffset.UtcNow;
        var jobId = Guid.NewGuid();
        var prompt = submission.Prompt.Trim();
        var targetProvider = ChooseExecutionProvider(submission, model);
        var embeddingSize = model.EmbeddingSize > 0 ? model.EmbeddingSize : 512;
        var embedding = GenerateDeterministicEmbedding(prompt, embeddingSize, model.Id);
        var chunks = BuildChunks(prompt, targetProvider, embedding);

        var job = new TensorJobStatusDto
        {
            Id = jobId,
            ModelId = model.Id,
            Status = "Completed",
            Prompt = prompt,
            PromptPreview = BuildPromptPreview(prompt),
            InputImageUrl = submission.InputImageUrl,
            ExecutionProvider = targetProvider,
            CreatedAt = now,
            CompletedAt = now,
            VectorDocumentId = null,
            Output = chunks,
            Metadata = new Dictionary<string, string>(submission.Metadata, StringComparer.OrdinalIgnoreCase)
        };

        if (submission.PersistToVectorStore)
        {
            var vectorId = await _vectorBridge.TryPersistAsync(job, embedding, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(vectorId))
            {
                job = job with { VectorDocumentId = vectorId };
                activity?.AddEvent(new ActivityEvent("tensor.vector.persisted"));
            }
        }

        await _store.UpsertAsync(job, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Tensor job {JobId} for model {ModelId} completed using {Provider}", job.Id, job.ModelId, job.ExecutionProvider);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return job;
    }

    private static string ChooseExecutionProvider(TensorJobSubmissionDto submission, TensorModelDescriptor model)
    {
        if (!string.IsNullOrWhiteSpace(submission.ExecutionProvider))
        {
            return submission.ExecutionProvider;
        }

        if (model.PreferredExecutionProviders.Count > 0)
        {
            return model.PreferredExecutionProviders[0];
        }

        return "wasm-cpu";
    }

    private static IList<TensorInferenceChunkDto> BuildChunks(string prompt, string provider, ReadOnlyMemory<float> embedding)
    {
        var vectorPreview = embedding.Span[..Math.Min(embedding.Length, 64)]
            .ToArray()
            .Select(value => Math.Round(value, 4))
            .ToArray();

        return new List<TensorInferenceChunkDto>
        {
            new()
            {
                Type = "text",
                Content = $"Prompt processed via {provider}. Length={prompt.Length} chars.",
                Sequence = 0,
                Confidence = 0.9
            },
            new()
            {
                Type = "vector",
                Content = JsonSerializer.Serialize(vectorPreview),
                Sequence = 1,
                Confidence = 0.95
            }
        };
    }

    private static ReadOnlyMemory<float> GenerateDeterministicEmbedding(string prompt, int size, string modelId)
    {
        var data = new float[Math.Max(8, size)];
        var utf8 = Encoding.UTF8.GetBytes($"{modelId}:{prompt}");
        var buffer = new byte[32];
        var cursor = 0;

        using var sha256 = SHA256.Create();
        var seed = utf8;
        while (cursor < data.Length)
        {
            buffer = sha256.ComputeHash(seed);
            seed = buffer;
            for (var index = 0; index < buffer.Length && cursor < data.Length; index += 4)
            {
                var slice = BitConverter.ToUInt32(buffer, index);
                data[cursor++] = (slice / (float)uint.MaxValue * 2f) - 1f;
            }
        }

        return new ReadOnlyMemory<float>(data);
    }

    private static string BuildPromptPreview(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        const int previewLength = 80;
        return prompt.Length <= previewLength
            ? prompt
            : string.Concat(prompt.AsSpan(0, previewLength), "…");
    }
}
