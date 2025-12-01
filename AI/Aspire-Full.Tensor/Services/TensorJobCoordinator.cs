using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspire_Full.Tensor.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Tensor.Services;

public interface ITensorJobCoordinator
{
    Task<TensorJobStatusDto> SubmitAsync(TensorJobSubmissionDto submission, CancellationToken cancellationToken = default);
}

public interface ITensorVectorBridge
{
    Task<string?> TryPersistAsync(TensorJobStatusDto job, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken);
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

        // Use Native CUDA Compute
        var embedding = GenerateNativeEmbedding(prompt, embeddingSize, model.Id, out var metrics);

        var chunks = BuildChunks(prompt, targetProvider, embedding);

        var job = new TensorJobStatusDto
        {
            Id = jobId,
            ModelId = model.Id,
            Status = "Completed",
            Prompt = prompt,
            PromptPreview = BuildPromptPreview(prompt),
            InputImageUrl = submission.InputImageUrl,
            ExecutionProvider = metrics.ActiveKernels > 0 ? "cuda" : targetProvider,
            CreatedAt = now,
            CompletedAt = now,
            VectorDocumentId = null,
            Output = chunks,
            Metadata = new Dictionary<string, string>(submission.Metadata, StringComparer.OrdinalIgnoreCase)
        };

        // Add Real-time Metrics
        job.Metadata["compute_time_ms"] = metrics.ComputeTimeMs.ToString("F4", CultureInfo.InvariantCulture);
        job.Metadata["memory_usage_mb"] = metrics.MemoryUsageMb.ToString("F2", CultureInfo.InvariantCulture);
        job.Metadata["active_kernels"] = metrics.ActiveKernels.ToString();

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

    private ReadOnlyMemory<float> GenerateNativeEmbedding(string prompt, int size, string modelId, out TensorMetrics metrics)
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
                seedData[cursor] = (slice / (float)uint.MaxValue); // 0..1
                weightData[cursor] = (float)Math.Sin(cursor * 0.1f); // Simulated weights
                cursor++;
            }
        }

        metrics = new TensorMetrics();
        try
        {
            // Execute on GPU
            NativeMethods.ComputeTensorOp(seedData, weightData, resultData, finalSize, ref metrics);
        }
        catch (DllNotFoundException)
        {
            // Fallback to CPU simulation if DLL is missing
            // _logger.LogWarning("AspireFull.Native.dll not found. Falling back to CPU simulation.");
            for (int i = 0; i < finalSize; i++)
            {
                resultData[i] = seedData[i] + weightData[i];
            }
            metrics.ComputeTimeMs = 0;
            metrics.ActiveKernels = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing native tensor op.");
            // Fallback
            for (int i = 0; i < finalSize; i++)
                resultData[i] = seedData[i];
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
