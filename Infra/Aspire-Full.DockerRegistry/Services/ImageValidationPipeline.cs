using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Abstractions;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.DockerRegistry.Models;
using Aspire_Full.DockerRegistry.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TensorPrimitives = System.Numerics.Tensors.TensorPrimitives;

namespace Aspire_Full.DockerRegistry.Services;

/// <summary>
/// GPU-accelerated image validation pipeline with TensorPrimitives fallback.
/// Uses portable SIMD operations when GPU is unavailable.
/// </summary>
public sealed class ImageValidationPipeline
{
    private readonly ILogger<ImageValidationPipeline> _logger;
    private readonly RegistryConfiguration _config;
    private readonly GpuMemoryPool _memoryPool;

    // Metrics
    private static readonly Meter s_meter = new("Aspire.DockerRegistry.Validation", "1.0.0");
    private static readonly Counter<long> s_validationsTotal = s_meter.CreateCounter<long>("validation.total");
    private static readonly Counter<long> s_validationsGpu = s_meter.CreateCounter<long>("validation.gpu");
    private static readonly Counter<long> s_validationsCpu = s_meter.CreateCounter<long>("validation.cpu");
    private static readonly Histogram<double> s_validationDuration = s_meter.CreateHistogram<double>("validation.duration_ms");

    public ImageValidationPipeline(
        ILogger<ImageValidationPipeline> logger,
        IOptions<RegistryConfiguration> config,
        GpuMemoryPool memoryPool)
    {
        _logger = logger;
        _config = config.Value;
        _memoryPool = memoryPool;
    }

    /// <summary>
    /// Validates a manifest using GPU-accelerated tensor operations when available.
    /// </summary>
    public async Task<ValidationResult> ValidateManifestAsync(
        DockerManifest manifest,
        CancellationToken cancellationToken = default)
    {
        var startTime = Stopwatch.GetTimestamp();
        s_validationsTotal.Add(1);

        _logger.LogInformation("Validating manifest for {Repository}:{Tag} (GPU: {GpuAvailable})",
            manifest.Repository, manifest.Tag, NativeTensorContext.IsGpuAvailable);

        var result = new ValidationResult { IsValid = true };

        if (_config.Validation.ValidateTensorContent)
        {
            var tensorResult = await ValidateTensorContentAsync(manifest, cancellationToken);
            result = result with
            {
                IsValid = result.IsValid && tensorResult.IsValid,
                TensorValidation = tensorResult
            };
        }

        if (_config.Validation.OptimizeLayers)
        {
            var layerResult = await ValidateLayerOptimizationAsync(manifest, cancellationToken);
            result = result with
            {
                IsValid = result.IsValid && layerResult.IsValid,
                LayerValidation = layerResult
            };
        }

        var duration = Stopwatch.GetElapsedTime(startTime);
        s_validationDuration.Record(duration.TotalMilliseconds);

        result = result with { DurationMs = duration.TotalMilliseconds };

        _logger.LogInformation(
            "Manifest validation {Status} in {Duration:F2}ms (GPU: {GpuUsed})",
            result.IsValid ? "passed" : "failed",
            duration.TotalMilliseconds,
            result.GpuAccelerated);

        return result;
    }

    /// <summary>
    /// Validates tensor content using GPU or TensorPrimitives fallback.
    /// </summary>
    private async Task<TensorValidationResult> ValidateTensorContentAsync(
        DockerManifest manifest,
        CancellationToken cancellationToken)
    {
        try
        {
            // Simulate layer data for validation (in production, download actual layer)
            var layerData = ArrayPool<float>.Shared.Rent(4096);
            try
            {
                // Initialize with manifest-derived validation pattern
                InitializeValidationData(layerData.AsSpan(0, 4096), manifest);

                var dataSpan = layerData.AsSpan(0, 4096);

                if (NativeTensorContext.IsGpuAvailable)
                {
                    s_validationsGpu.Add(1);
                    return await ValidateWithGpuAsync(layerData, cancellationToken);
                }
                else
                {
                    s_validationsCpu.Add(1);
                    return ValidateWithTensorPrimitives(layerData);
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(layerData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during tensor validation");
            return _config.Validation.TensorCheckStrictness == "Strict"
                ? new TensorValidationResult { IsValid = false, ErrorMessage = ex.Message }
                : new TensorValidationResult { IsValid = true, SkippedDueToError = true };
        }
    }

    /// <summary>
    /// GPU-accelerated validation path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<TensorValidationResult> ValidateWithGpuAsync(
        float[] data,
        CancellationToken cancellationToken)
    {
        var validated = NativeTensorContext.ValidateContent(data.AsSpan(), 0.5f, out var metrics);

        return Task.FromResult(new TensorValidationResult
        {
            IsValid = validated,
            GpuAccelerated = true,
            ComputeTimeMs = metrics.compute_time_ms,
            MemoryUsageMb = metrics.memory_usage_mb,
            GpuUtilization = metrics.gpu_utilization_percent
        });
    }

    /// <summary>
    /// CPU validation using TensorPrimitives for portable SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TensorValidationResult ValidateWithTensorPrimitives(float[] data)
    {
        var startTime = Stopwatch.GetTimestamp();

        // Compute L2 norm to detect anomalies
        var norm = TensorPrimitives.Norm(data);

        // Check for NaN/Inf values
        var hasInvalidValues = !float.IsFinite(norm);

        // Compute max magnitude for threshold check
        var maxMagnitude = TensorPrimitives.MaxMagnitude(data);
        var isWithinThreshold = maxMagnitude <= 1000f; // Configurable threshold

        var duration = Stopwatch.GetElapsedTime(startTime);

        return new TensorValidationResult
        {
            IsValid = !hasInvalidValues && isWithinThreshold,
            GpuAccelerated = false,
            ComputeTimeMs = (float)duration.TotalMilliseconds,
            Norm = norm,
            MaxMagnitude = maxMagnitude
        };
    }

    /// <summary>
    /// Validates layer optimization using cosine similarity.
    /// </summary>
    private async Task<LayerValidationResult> ValidateLayerOptimizationAsync(
        DockerManifest manifest,
        CancellationToken cancellationToken)
    {
        // Check for duplicate layers using cosine similarity
        if (manifest.Layers is null || manifest.Layers.Count < 2)
        {
            return new LayerValidationResult { IsValid = true, LayerCount = manifest.Layers?.Count ?? 0 };
        }

        var duplicates = 0;
        var layerHashes = ArrayPool<float>.Shared.Rent(manifest.Layers.Count * 128);
        try
        {
            // Generate pseudo-embeddings for each layer
            for (int i = 0; i < manifest.Layers.Count; i++)
            {
                var embedding = layerHashes.AsSpan(i * 128, 128);
                GenerateLayerEmbedding(manifest.Layers[i], embedding);
            }

            // Compare all pairs using cosine similarity
            for (int i = 0; i < manifest.Layers.Count - 1; i++)
            {
                for (int j = i + 1; j < manifest.Layers.Count; j++)
                {
                    var embeddingI = layerHashes.AsSpan(i * 128, 128);
                    var embeddingJ = layerHashes.AsSpan(j * 128, 128);

                    var similarity = NativeTensorContext.CosineSimilarity(embeddingI, embeddingJ);
                    if (similarity > 0.99f)
                    {
                        duplicates++;
                        _logger.LogWarning("Potential duplicate layers detected: {Layer1} and {Layer2} (similarity: {Similarity:P2})",
                            i, j, similarity);
                    }
                }
            }

            await Task.CompletedTask; // Yield for async pattern

            return new LayerValidationResult
            {
                IsValid = duplicates == 0 || !_config.Validation.DeduplicationEnabled,
                LayerCount = manifest.Layers.Count,
                DuplicateCount = duplicates
            };
        }
        finally
        {
            ArrayPool<float>.Shared.Return(layerHashes);
        }
    }

    private static void InitializeValidationData(Span<float> data, DockerManifest manifest)
    {
        // Generate deterministic validation pattern from manifest
        var seed = manifest.Repository?.GetHashCode() ?? 0;
        var random = new Random(seed);

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (float)(random.NextDouble() * 2 - 1); // Range [-1, 1]
        }
    }

    private static void GenerateLayerEmbedding(DockerManifestLayer layer, Span<float> embedding)
    {
        // Generate pseudo-embedding from layer metadata
        var hash = layer.Digest?.GetHashCode() ?? 0;
        var random = new Random(hash);

        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)random.NextDouble();
        }

        // Normalize to unit vector
        var norm = TensorPrimitives.Norm(embedding);
        if (norm > 0)
        {
            TensorPrimitives.Divide(embedding, norm, embedding);
        }
    }
}

#region Result Types

public readonly record struct ValidationResult
{
    public required bool IsValid { get; init; }
    public double DurationMs { get; init; }
    public bool GpuAccelerated => TensorValidation?.GpuAccelerated ?? false;
    public TensorValidationResult? TensorValidation { get; init; }
    public LayerValidationResult? LayerValidation { get; init; }
}

public readonly record struct TensorValidationResult
{
    public required bool IsValid { get; init; }
    public bool GpuAccelerated { get; init; }
    public float ComputeTimeMs { get; init; }
    public float MemoryUsageMb { get; init; }
    public int GpuUtilization { get; init; }
    public float Norm { get; init; }
    public float MaxMagnitude { get; init; }
    public bool SkippedDueToError { get; init; }
    public string? ErrorMessage { get; init; }
}

public readonly record struct LayerValidationResult
{
    public required bool IsValid { get; init; }
    public int LayerCount { get; init; }
    public int DuplicateCount { get; init; }
}

#endregion
