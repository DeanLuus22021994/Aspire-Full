using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspire_Full.Shared.Models;
using Aspire_Full.Tensor.Core.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Tensor.Core.Orchestration;

/// <summary>
/// Interface for submitting tensor inference jobs.
/// </summary>
public interface ITensorJobCoordinator
{
    Task<TensorJobStatus> SubmitAsync(TensorJobSubmission submission, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for persisting tensor job embeddings to vector storage.
/// </summary>
public interface ITensorVectorBridge
{
    Task<string?> TryPersistAsync(TensorJobStatus job, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken);
}

/// <summary>
/// Coordinates tensor job submission, execution, and persistence.
/// Uses native CUDA compute with automatic CPU fallback.
/// </summary>
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
        var descriptorList = catalogOptions.Value.Models ?? [];
        _catalog = descriptorList.ToDictionary(model => model.Id, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<TensorJobStatus> SubmitAsync(TensorJobSubmission submission, CancellationToken cancellationToken = default)
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

        // Use Native CUDA Compute
        var embedding = GenerateNativeEmbedding(prompt, embeddingSize, model.Id, out var metrics);

        var chunks = BuildChunks(prompt, targetProvider, embedding);

        var metadata = new Dictionary<string, string>(submission.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["compute_time_ms"] = metrics.compute_time_ms.ToString("F4", CultureInfo.InvariantCulture),
            ["memory_usage_mb"] = metrics.memory_usage_mb.ToString("F2", CultureInfo.InvariantCulture),
            ["active_kernels"] = metrics.active_kernels.ToString()
        };

        var job = new TensorJobStatus
        {
            Id = jobId,
            ModelId = model.Id,
            Status = "Completed",
            Prompt = prompt,
            PromptPreview = BuildPromptPreview(prompt),
            InputImageUrl = submission.InputImageUrl,
            ExecutionProvider = metrics.active_kernels > 0 ? "cuda" : targetProvider,
            CreatedAt = now,
            CompletedAt = now,
            VectorDocumentId = null,
            Output = chunks,
            Metadata = metadata
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

    private static string ChooseExecutionProvider(TensorJobSubmission submission, TensorModelDescriptor model)
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

    private static IList<TensorInferenceChunk> BuildChunks(string prompt, string provider, ReadOnlyMemory<float> embedding)
    {
        var vectorPreview = embedding.Span[..Math.Min(embedding.Length, 64)]
            .ToArray()
            .Select(value => Math.Round(value, 4))
            .ToArray();

        return
        [
            new TensorInferenceChunk
            {
                Type = "text",
                Content = $"Prompt processed via {provider}. Length={prompt.Length} chars.",
                Sequence = 0,
                Confidence = 0.9
            },
            new TensorInferenceChunk
            {
                Type = "vector",
                Content = JsonSerializer.Serialize(vectorPreview),
                Sequence = 1,
                Confidence = 0.95
            }
        ];
    }

    private ReadOnlyMemory<float> GenerateNativeEmbedding(string prompt, int size, string modelId, out NativeTensorContext.TensorMetrics metrics)
    {
        var finalSize = Math.Max(8, size);
        var seedData = new float[finalSize];
        var weightData = new float[finalSize];
        var resultData = new float[finalSize];

        // Initialize input vectors deterministically based on prompt
        var utf8 = Encoding.UTF8.GetBytes($"{modelId}:{prompt}");
        using var sha256 = SHA256.Create();
        var seed = utf8;
        var cursor = 0;

        // Fill seedData
        while (cursor < finalSize)
        {
            var buffer = sha256.ComputeHash(seed);
            seed = buffer;
            for (var index = 0; index < buffer.Length && cursor < finalSize; index += 4)
            {
                var slice = BitConverter.ToUInt32(buffer, index);
                seedData[cursor] = slice / (float)uint.MaxValue; // 0..1
                weightData[cursor] = (float)Math.Sin(cursor * 0.1f); // Simulated weights
                cursor++;
            }
        }

        metrics = new NativeTensorContext.TensorMetrics();
        try
        {
            // Execute on GPU
            NativeTensorContext.ComputeTensorOp(seedData, weightData, resultData, finalSize, ref metrics);
        }
        catch (DllNotFoundException)
        {
            // Fallback to CPU simulation if DLL is missing
            for (var i = 0; i < finalSize; i++)
            {
                resultData[i] = seedData[i] + weightData[i];
            }
            metrics.compute_time_ms = 0;
            metrics.active_kernels = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing native tensor op.");
            // Fallback
            for (var i = 0; i < finalSize; i++)
            {
                resultData[i] = seedData[i];
            }
        }

        return new ReadOnlyMemory<float>(resultData);
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
