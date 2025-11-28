using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Aspire_Full.VectorStore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Connectors;

public interface IVectorStoreConnector
{
    Task<VectorStoreConnectorResult> UpsertAsync(VectorStoreConnectorRequest request, CancellationToken cancellationToken = default);
}

public sealed record VectorStoreConnectorRequest(
    string? DocumentId,
    string Content,
    ReadOnlyMemory<float> Embedding,
    IReadOnlyDictionary<string, object?> Metadata);

public sealed record VectorStoreConnectorResult(bool Success, string? DocumentId, string? Error);

internal sealed class VectorStoreConnector : IVectorStoreConnector
{
    private const string EfCoreLatencyKey = "efcore_latency_ms";
    private const string CodeQualityKey = "code_quality_score";
    private const string VolumeOffloadKey = "volume_offload_ratio";
    private const string RedundancyKey = "redundancy_factor";
    private const string ResourceUtilizationKey = "resource_utilization";
    private const string PollutionKey = "codebase_pollution_score";
    private const string VectorQualityKey = "vector_quality_score";
    private const string TestingAcceptanceKey = "testing_acceptance";
    private const string AlertingLatencyKey = "alerting_latency_ms";
    private const string GpuOnlyKey = "gpu_only";
    private const string CpuComputationKey = "cpu_computation_ms";
    private const string ExecutionProviderKey = "execution_provider";
    private const string CreatedAtKey = "created_at";
    private const string OperationTimestampKey = "operation.timestamp";
    private const string OperationSuccessKey = "operation.success";
    private const string OperationErrorKey = "operation.error";
    private const string EmbeddingLengthKey = "embedding.length";
    private const string ContentLengthKey = "content.length";

    private readonly IVectorStoreService _vectorStoreService;
    private readonly ILogger<VectorStoreConnector> _logger;
    private readonly IConnectorHealthRegistry _healthRegistry;
    private readonly IConnectorMetricReporter _metricReporter;
    private readonly string _collectionName;

    public VectorStoreConnector(
        IVectorStoreService vectorStoreService,
        IOptions<ConnectorHubOptions> options,
        IConnectorHealthRegistry healthRegistry,
        IConnectorMetricReporter metricReporter,
        ILogger<VectorStoreConnector> logger)
    {
        _vectorStoreService = vectorStoreService;
        _logger = logger;
        _healthRegistry = healthRegistry;
        _metricReporter = metricReporter;
        _collectionName = options.Value.VectorStore.CollectionName;
    }

    public async Task<VectorStoreConnectorResult> UpsertAsync(VectorStoreConnectorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var documentId = string.IsNullOrWhiteSpace(request.DocumentId) ? Guid.NewGuid().ToString() : request.DocumentId;
        using var activity = ConnectorDiagnostics.ActivitySource.StartActivity("VectorStoreConnector.Upsert");
        activity?.SetTag("connector.collection", _collectionName);
        activity?.SetTag("connector.embedding_length", request.Embedding.Length);

        try
        {
            var document = new VectorDocument
            {
                Id = documentId,
                Content = request.Content,
                Embedding = request.Embedding,
                Metadata = request.Metadata?.ToDictionary(static pair => pair.Key, static pair => pair.Value ?? string.Empty)
            };

            await _vectorStoreService.EnsureCollectionAsync(_collectionName, document.Embedding.Length, cancellationToken).ConfigureAwait(false);
            await _vectorStoreService.UpsertAsync(document, cancellationToken).ConfigureAwait(false);

            _healthRegistry.ReportHealthy("vector-store", $"Last upsert at {DateTimeOffset.UtcNow:O}");
            await PublishMetricSuiteAsync(request, documentId, success: true, error: null, cancellationToken).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return new VectorStoreConnectorResult(true, documentId, null);
        }
        catch (Exception ex)
        {
            _healthRegistry.ReportUnhealthy("vector-store", ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                ["exception.type"] = ex.GetType().FullName ?? "Exception",
                ["exception.message"] = ex.Message
            }));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Vector store upsert failed");
            await PublishMetricSuiteAsync(request, documentId, success: false, error: ex.Message, cancellationToken).ConfigureAwait(false);
            return new VectorStoreConnectorResult(false, null, ex.Message);
        }
    }

    private async Task PublishMetricSuiteAsync(VectorStoreConnectorRequest request, string documentId, bool success, string? error, CancellationToken cancellationToken)
    {
        var metadata = BuildMetricMetadata(request, documentId, success, error);
        var context = new MetricContext(request, metadata, success, error);

        foreach (var report in BuildMetricReports(context))
        {
            await _metricReporter.ReportAsync(report, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyDictionary<string, string> BuildMetricMetadata(VectorStoreConnectorRequest request, string documentId, bool success, string? error)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["document.id"] = documentId,
            [OperationSuccessKey] = success ? "true" : "false",
            [OperationTimestampKey] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            [EmbeddingLengthKey] = request.Embedding.Length.ToString(CultureInfo.InvariantCulture),
            [ContentLengthKey] = (request.Content?.Length ?? 0).ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(error))
        {
            metadata[OperationErrorKey] = error!;
        }

        if (request.Metadata is { Count: > 0 })
        {
            foreach (var pair in request.Metadata)
            {
                var value = FormatMetadataValue(pair.Value);
                metadata[pair.Key] = value;
                metadata.TryAdd($"request.{pair.Key}", value);
            }
        }

        var vectorQuality = ComputeVectorQuality(request.Embedding);
        metadata[VectorQualityKey] = vectorQuality.ToString("0.###", CultureInfo.InvariantCulture);

        if (!metadata.ContainsKey(ResourceUtilizationKey))
        {
            var resourceScore = ComputeResourceUtilization(request, metadata);
            metadata[ResourceUtilizationKey] = resourceScore.ToString("0.###", CultureInfo.InvariantCulture);
        }

        if (!metadata.ContainsKey(ExecutionProviderKey))
        {
            metadata[ExecutionProviderKey] = "unknown";
        }

        return metadata;
    }

    private static IEnumerable<ConnectorMetricReport> BuildMetricReports(MetricContext context)
    {
        yield return BuildEfCoreMetric(context);
        yield return BuildCodeQualityMetric(context);
        yield return BuildNamedVolumeMetric(context);
        yield return BuildRedundancyMetric(context);
        yield return BuildResourceMetric(context);
        yield return BuildCodebasePollutionMetric(context);
        yield return BuildVectorQualityMetric(context);
        yield return BuildTestingMetric(context);
        yield return BuildAlertingMetric(context);
        yield return BuildGpuOnlyMetric(context);
        yield return BuildCpuMetric(context);
    }

    private static ConnectorMetricReport BuildEfCoreMetric(MetricContext context)
    {
        var latency = TryGetDouble(context.Metadata, EfCoreLatencyKey);
        var score = latency.HasValue ? NormalizeLatency(latency.Value) : DefaultScore(context.Success, 0.9, 0.35);
        var detail = latency.HasValue
            ? $"EF Core latency {latency.Value:F1} ms."
            : $"EF Core latency not supplied; inferred from success={context.Success}.";
        return CreateReport(ConnectorMetricDimension.EfCore, score, detail, context.Metadata);
    }

    private static ConnectorMetricReport BuildCodeQualityMetric(MetricContext context)
    {
        var score = ResolveCodeQuality(context, out var fromMetadata);
        var detail = fromMetadata
            ? "Code quality supplied via connector metadata."
            : "Code quality derived from prompt density heuristics.";
        return CreateReport(ConnectorMetricDimension.CodeQuality, score, detail, context.Metadata);
    }

    private static ConnectorMetricReport BuildNamedVolumeMetric(MetricContext context)
    {
        var ratio = TryGetDouble(context.Metadata, VolumeOffloadKey) ?? ComputeVolumeOffloadRatio(context.Request);
        var detail = context.Metadata.ContainsKey(VolumeOffloadKey)
            ? "Named volume ratio supplied via metadata."
            : "Named volume ratio derived from payload vs. embedding size.";
        return CreateReport(ConnectorMetricDimension.NamedVolumeEfficiency, ratio, detail, context.Metadata);
    }

    private static ConnectorMetricReport BuildRedundancyMetric(MetricContext context)
    {
        var rawFactor = TryGetDouble(context.Metadata, RedundancyKey) ?? ComputeRedundancyFactor(context.Request);
        var normalized = Math.Clamp(rawFactor / 3d, 0, 1);
        var detail = context.Metadata.ContainsKey(RedundancyKey)
            ? $"Redundancy factor {rawFactor:0.##}."
            : "Redundancy inferred from metadata coverage.";
        return CreateReport(ConnectorMetricDimension.Redundancy, normalized, detail, context.Metadata);
    }

    private static ConnectorMetricReport BuildResourceMetric(MetricContext context)
    {
        var score = TryGetDouble(context.Metadata, ResourceUtilizationKey) ?? ComputeResourceUtilization(context.Request, context.Metadata);
        var detail = context.Metadata.ContainsKey(ResourceUtilizationKey)
            ? "Resource utilization supplied via metadata."
            : "Resource utilization derived from execution provider and embedding size.";
        return CreateReport(ConnectorMetricDimension.ResourceUtilization, score, detail, context.Metadata);
    }

    private static ConnectorMetricReport BuildCodebasePollutionMetric(MetricContext context)
    {
        if (TryGetDouble(context.Metadata, PollutionKey) is { } pollutionScore)
        {
            var normalized = Math.Clamp(1 - pollutionScore, 0, 1);
            var detail = "Pollution score provided via metadata.";
            return CreateReport(ConnectorMetricDimension.CodebasePollution, normalized, detail, context.Metadata);
        }

        var quality = ResolveCodeQuality(context, out _);
        var derived = Math.Clamp(quality, 0, 1);
        return CreateReport(ConnectorMetricDimension.CodebasePollution, derived, "Pollution derived from code quality heuristics.", context.Metadata);
    }

    private static ConnectorMetricReport BuildVectorQualityMetric(MetricContext context)
    {
        var score = TryGetDouble(context.Metadata, VectorQualityKey) ?? ComputeVectorQuality(context.Request.Embedding);
        var detail = context.Metadata.ContainsKey(VectorQualityKey)
            ? "Vector quality carried via metadata."
            : "Vector quality recomputed from embedding variance.";
        return CreateReport(ConnectorMetricDimension.VectorStoreQuality, score, detail, context.Metadata);
    }

    private static ConnectorMetricReport BuildTestingMetric(MetricContext context)
    {
        var score = TryGetDouble(context.Metadata, TestingAcceptanceKey) ?? ComputeTestingAcceptance(context);
        var detail = context.Metadata.ContainsKey(TestingAcceptanceKey)
            ? "Testing efficiency supplied via metadata."
            : "Testing efficiency derived from metadata breadth and success state.";
        return CreateReport(ConnectorMetricDimension.TestingEfficiency, score, detail, context.Metadata);
    }

    private static ConnectorMetricReport BuildAlertingMetric(MetricContext context)
    {
        if (TryGetDouble(context.Metadata, AlertingLatencyKey) is { } latencyMs)
        {
            var score = NormalizeLatency(latencyMs);
            var detail = $"Alerting latency {latencyMs:F1} ms.";
            return CreateReport(ConnectorMetricDimension.AlertingReliability, score, detail, context.Metadata);
        }

        var computed = ComputeAlertingLatencyScore(context.Metadata, context.Success);
        return CreateReport(ConnectorMetricDimension.AlertingReliability, computed, "Alerting latency derived from timestamps.", context.Metadata);
    }

    private static ConnectorMetricReport BuildGpuOnlyMetric(MetricContext context)
    {
        var gpuOnly = TryGetBool(context.Metadata, GpuOnlyKey);
        if (gpuOnly.HasValue)
        {
            var score = gpuOnly.Value ? 1.0 : 0.4;
            var detail = gpuOnly.Value ? "GPU-only workload flagged." : "GPU-only flag absent.";
            return CreateReport(ConnectorMetricDimension.GpuOnlyAlerting, score, detail, context.Metadata);
        }

        var provider = ResolveExecutionProvider(context.Metadata);
        var derivedScore = provider.Contains("gpu", StringComparison.OrdinalIgnoreCase) ? 0.95 : 0.5;
        var derivedDetail = provider.Contains("gpu", StringComparison.OrdinalIgnoreCase)
            ? "GPU provider implies GPU flag."
            : "GPU provider missing; CPU fallback assumed.";
        return CreateReport(ConnectorMetricDimension.GpuOnlyAlerting, derivedScore, derivedDetail, context.Metadata);
    }

    private static ConnectorMetricReport BuildCpuMetric(MetricContext context)
    {
        var cpuCost = TryGetDouble(context.Metadata, CpuComputationKey)
            ?? EstimateCpuCost(context.Request, context.Metadata);
        var score = ComputeCpuCostScore(cpuCost);
        var detail = context.Metadata.ContainsKey(CpuComputationKey)
            ? $"CPU computation cost {cpuCost:F1} ms." : "CPU computation derived from embedding size.";
        return CreateReport(ConnectorMetricDimension.CpuComputationAnalysis, score, detail, context.Metadata);
    }

    private static ConnectorMetricReport CreateReport(ConnectorMetricDimension dimension, double score, string detail, IReadOnlyDictionary<string, string> metadata)
    {
        var normalized = Math.Clamp(score, 0, 1);
        return new ConnectorMetricReport(dimension, normalized, ConnectorMetricStatusHelper.FromScore(normalized), detail, metadata);
    }

    private static double NormalizeLatency(double latencyMs)
    {
        if (latencyMs <= 25)
        {
            return 1.0;
        }

        if (latencyMs <= 50)
        {
            return 0.9;
        }

        if (latencyMs <= 100)
        {
            return 0.75;
        }

        if (latencyMs <= 200)
        {
            return 0.55;
        }

        return 0.3;
    }

    private static double ResolveCodeQuality(MetricContext context, out bool fromMetadata)
    {
        if (TryGetDouble(context.Metadata, CodeQualityKey) is { } metadataScore)
        {
            fromMetadata = true;
            return Math.Clamp(metadataScore, 0, 1);
        }

        fromMetadata = false;
        return ComputeCodeQualityScore(context.Request.Content);
    }

    private static double ComputeCodeQualityScore(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0.5;
        }

        var span = content.AsSpan();
        var sampleLength = Math.Min(span.Length, 1200);
        var letters = 0;
        var digits = 0;

        for (var index = 0; index < sampleLength; index++)
        {
            var ch = span[index];
            if (char.IsLetter(ch))
            {
                letters++;
            }
            else if (char.IsDigit(ch))
            {
                digits++;
            }
        }

        var ratio = (letters + digits) / (double)Math.Max(1, sampleLength);
        return Math.Clamp(ratio, 0.2, 1);
    }

    private static double ComputeVolumeOffloadRatio(VectorStoreConnectorRequest request)
    {
        var contentLength = Math.Max(1, request.Content?.Length ?? 1);
        var embeddingLength = Math.Max(1, request.Embedding.Length);
        var ratio = embeddingLength / (double)(embeddingLength + contentLength);
        return Math.Clamp(ratio, 0, 1);
    }

    private static double ComputeRedundancyFactor(VectorStoreConnectorRequest request)
    {
        var metadataCount = request.Metadata?.Count ?? 0;
        return Math.Clamp(1 + metadataCount / 3d, 1, 3);
    }

    private static double ComputeResourceUtilization(VectorStoreConnectorRequest request, IReadOnlyDictionary<string, string> metadata)
    {
        var provider = ResolveExecutionProvider(metadata);
        var baseScore = provider.Contains("gpu", StringComparison.OrdinalIgnoreCase) ? 0.85 : provider.Contains("cpu", StringComparison.OrdinalIgnoreCase) ? 0.65 : 0.75;
        var dimensionBonus = Math.Clamp(request.Embedding.Length / 8192d, 0, 0.15);
        return Math.Clamp(baseScore + dimensionBonus, 0, 1);
    }

    private static double ComputeVectorQuality(ReadOnlyMemory<float> embedding)
    {
        if (embedding.IsEmpty)
        {
            return 0.0;
        }

        var span = embedding.Span;
        double sum = 0;
        double sumSquares = 0;

        for (var index = 0; index < span.Length; index++)
        {
            var value = span[index];
            sum += value;
            sumSquares += value * value;
        }

        var mean = sum / span.Length;
        var variance = Math.Max(0, (sumSquares / span.Length) - (mean * mean));
        var stdDev = Math.Sqrt(variance);
        return Math.Clamp(stdDev / 0.75, 0, 1);
    }

    private static double ComputeTestingAcceptance(MetricContext context)
    {
        var metadataCount = context.Request.Metadata?.Count ?? 0;
        var baseScore = Math.Clamp(0.7 + metadataCount * 0.025, 0, 1);
        return context.Success ? baseScore : Math.Max(0.3, baseScore - 0.25);
    }

    private static double ComputeAlertingLatencyScore(IReadOnlyDictionary<string, string> metadata, bool success)
    {
        var created = TryGetDateTime(metadata, CreatedAtKey);
        var operation = TryGetDateTime(metadata, OperationTimestampKey);
        if (created.HasValue && operation.HasValue)
        {
            var latency = Math.Abs((operation.Value - created.Value).TotalMilliseconds);
            return NormalizeLatency(Math.Max(latency, 1));
        }

        return success ? 0.8 : 0.45;
    }

    private static double EstimateCpuCost(VectorStoreConnectorRequest request, IReadOnlyDictionary<string, string> metadata)
    {
        var provider = ResolveExecutionProvider(metadata);
        var baseCost = provider.Contains("gpu", StringComparison.OrdinalIgnoreCase) ? 4 : 12;
        var dimensionCost = request.Embedding.Length * 0.015;
        return Math.Max(1, baseCost + dimensionCost);
    }

    private static double ComputeCpuCostScore(double cpuCostMs)
    {
        if (cpuCostMs <= 10)
        {
            return 1.0;
        }

        if (cpuCostMs <= 25)
        {
            return 0.85;
        }

        if (cpuCostMs <= 50)
        {
            return 0.65;
        }

        return 0.4;
    }

    private static double DefaultScore(bool success, double successScore, double failureScore)
        => success ? successScore : failureScore;

    private static double? TryGetDouble(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (metadata.TryGetValue(key, out var raw) &&
            double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    private static bool? TryGetBool(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (metadata.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value))
        {
            return value;
        }

        return null;
    }

    private static DateTimeOffset? TryGetDateTime(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (metadata.TryGetValue(key, out var raw) && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
        {
            return value;
        }

        return null;
    }

    private static string ResolveExecutionProvider(IReadOnlyDictionary<string, string> metadata)
        => metadata.TryGetValue(ExecutionProviderKey, out var provider) && !string.IsNullOrWhiteSpace(provider)
            ? provider
            : "unknown";

    private static string FormatMetadataValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private readonly record struct MetricContext(
        VectorStoreConnectorRequest Request,
        IReadOnlyDictionary<string, string> Metadata,
        bool Success,
        string? Error);
    }
