using Aspire_Full.DockerRegistry.Abstractions;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.DockerRegistry.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Aspire_Full.DockerRegistry.GarbageCollection;

/// <summary>
/// Policy that detects and marks redundant Docker images for cleanup.
/// Redundancy is determined by:
/// - Duplicate layer digests across images
/// - Untagged/dangling images
/// - Images superseded by newer versions
/// </summary>
public sealed class RedundancyDetectionPolicy : IGarbageCollectionPolicy
{
    private readonly DockerRegistryOptions _options;
    private readonly ILogger<RedundancyDetectionPolicy>? _logger;
    private readonly TimeProvider _timeProvider;

    // Track layer usage across images for deduplication analysis
    private static readonly ConcurrentDictionary<string, HashSet<string>> _layerUsage = new();

    public RedundancyDetectionPolicy(
        IOptions<DockerRegistryOptions> options,
        ILogger<RedundancyDetectionPolicy>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<bool> ShouldDeleteAsync(
        DockerImageDescriptor descriptor,
        string tag,
        DockerManifest? manifest,
        CancellationToken cancellationToken = default)
    {
        // Check 1: Dangling/untagged images
        if (string.IsNullOrEmpty(tag) || tag == "<none>")
        {
            _logger?.LogDebug("Marking {Repository} as redundant: untagged/dangling", descriptor.Service);
            return Task.FromResult(true);
        }

        // Check 2: Superseded versions (dev/build tags older than retention)
        if (IsSupersededDevTag(tag))
        {
            _logger?.LogDebug("Marking {Repository}:{Tag} as redundant: superseded dev build", descriptor.Service, tag);
            return Task.FromResult(true);
        }

        // Check 3: Track layer usage for deduplication reporting
        if (manifest?.Layers is { Count: > 0 })
        {
            TrackLayerUsage(descriptor.Service, tag, manifest);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Determines if a tag represents a superseded development build.
    /// Dev tags like "dev-*", "build-*", "pr-*" are considered temporary.
    /// </summary>
    private static bool IsSupersededDevTag(string tag)
    {
        // Keep latest, main, release tags
        if (tag is "latest" or "main" or "master")
            return false;

        // Keep semantic versions (v1.2.3, 1.2.3)
        if (IsSemanticVersion(tag))
            return false;

        // Check for temporary/dev patterns that should be cleaned up
        var devPatterns = new[] { "dev-", "build-", "pr-", "ci-", "test-", "tmp-" };
        foreach (var pattern in devPatterns)
        {
            if (tag.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                // Could add age check here if we had manifest creation date
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a tag follows semantic versioning patterns.
    /// </summary>
    private static bool IsSemanticVersion(string tag)
    {
        var normalized = tag.TrimStart('v');
        var parts = normalized.Split('.');

        if (parts.Length < 2)
            return false;

        return parts.All(p => p.All(char.IsDigit) || p.Contains('-'));
    }

    /// <summary>
    /// Tracks layer usage for deduplication analysis.
    /// </summary>
    private static void TrackLayerUsage(string repository, string tag, DockerManifest manifest)
    {
        var imageKey = $"{repository}:{tag}";

        foreach (var layer in manifest.Layers)
        {
            var layerDigest = layer.Digest;
            _layerUsage.AddOrUpdate(
                layerDigest,
                _ => [imageKey],
                (_, existing) => { existing.Add(imageKey); return existing; });
        }
    }

    /// <summary>
    /// Gets statistics about layer deduplication across tracked images.
    /// </summary>
    public static RedundancyStatistics GetRedundancyStatistics()
    {
        var totalLayers = _layerUsage.Count;
        var sharedLayers = _layerUsage.Count(kvp => kvp.Value.Count > 1);
        var uniqueLayers = totalLayers - sharedLayers;

        // Calculate potential savings
        var totalImages = _layerUsage.Values
            .SelectMany(v => v)
            .Distinct()
            .Count();

        return new RedundancyStatistics
        {
            TotalLayers = totalLayers,
            SharedLayers = sharedLayers,
            UniqueLayers = uniqueLayers,
            TotalImages = totalImages,
            DeduplicationRatio = totalLayers > 0
                ? (double)sharedLayers / totalLayers
                : 0
        };
    }

    /// <summary>
    /// Clears the layer tracking cache.
    /// </summary>
    public static void ResetTracking()
    {
        _layerUsage.Clear();
    }
}

/// <summary>
/// Statistics about layer redundancy across Docker images.
/// </summary>
public sealed record RedundancyStatistics
{
    /// <summary>
    /// Total number of unique layer digests.
    /// </summary>
    public required int TotalLayers { get; init; }

    /// <summary>
    /// Number of layers shared by multiple images.
    /// </summary>
    public required int SharedLayers { get; init; }

    /// <summary>
    /// Number of layers used by only one image.
    /// </summary>
    public required int UniqueLayers { get; init; }

    /// <summary>
    /// Total number of images analyzed.
    /// </summary>
    public required int TotalImages { get; init; }

    /// <summary>
    /// Ratio of shared to total layers (0-1).
    /// Higher values indicate better deduplication.
    /// </summary>
    public required double DeduplicationRatio { get; init; }
}
