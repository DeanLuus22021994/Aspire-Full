using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Tensor.Core.Models;

/// <summary>
/// Represents a registered model with version tracking and metadata.
/// </summary>
public sealed record ModelInfo
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required ModelType Type { get; init; }
    public required long SizeBytes { get; init; }
    public required string Path { get; init; }
    public DateTimeOffset LoadedAt { get; init; }
    public DateTimeOffset LastAccessedAt { get; set; }
    public long AccessCount { get; set; }
    public bool IsLoaded { get; init; }
    public ModelDevice Device { get; init; } = ModelDevice.Cpu;
}

/// <summary>
/// Model type classification for registry management.
/// </summary>
public enum ModelType
{
    Embedding,
    Classification,
    FaceRecognition,
    ObjectDetection,
    LanguageModel,
    Custom
}

/// <summary>
/// Target device for model inference.
/// </summary>
public enum ModelDevice
{
    Cpu,
    Cuda,
    CudaHalf,
    Onnx
}

/// <summary>
/// Cache eviction policies for the model registry.
/// </summary>
public enum EvictionPolicy
{
    /// <summary>Least Recently Used - evicts models that haven't been accessed recently.</summary>
    Lru,
    /// <summary>Least Frequently Used - evicts models with lowest access count.</summary>
    Lfu,
    /// <summary>First In First Out - evicts oldest loaded models.</summary>
    Fifo,
    /// <summary>Size-based - evicts largest models first to free memory quickly.</summary>
    SizeBased
}

/// <summary>
/// Configuration options for the model registry.
/// </summary>
public sealed class ModelRegistryOptions
{
    /// <summary>Directory where models are cached on disk. Uses shared mount.</summary>
    public string CacheDirectory { get; set; } = "/shared/models";

    /// <summary>Maximum number of models to keep in memory.</summary>
    public int MaxCachedModels { get; set; } = 10;

    /// <summary>Maximum total memory for cached models in bytes.</summary>
    public long MaxCacheMemoryBytes { get; set; } = 4L * 1024 * 1024 * 1024; // 4GB default

    /// <summary>Policy for evicting models when cache is full.</summary>
    public EvictionPolicy EvictionPolicy { get; set; } = EvictionPolicy.Lru;

    /// <summary>Whether to track version history for models.</summary>
    public bool TrackVersions { get; set; } = true;

    /// <summary>Maximum versions to retain per model.</summary>
    public int MaxVersionsPerModel { get; set; } = 3;

    /// <summary>Enable automatic preloading of high-priority models.</summary>
    public bool EnablePreloading { get; set; } = true;
}

/// <summary>
/// Model registry for tracking, caching, and managing AI model versions in a PaaS environment.
/// Provides efficient model lifecycle management with configurable eviction policies.
/// </summary>
public sealed class ModelRegistry : IModelRegistry, IDisposable
{
    private readonly ConcurrentDictionary<string, ModelInfo> _loadedModels = new();
    private readonly ConcurrentDictionary<string, List<ModelInfo>> _versionHistory = new();
    private readonly ModelRegistryOptions _options;
    private readonly ILogger<ModelRegistry> _logger;
    private readonly SemaphoreSlim _evictionLock = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private bool _disposed;

    public ModelRegistry(
        IOptions<ModelRegistryOptions> options,
        ILogger<ModelRegistry> logger,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public int LoadedModelCount => _loadedModels.Count;

    /// <inheritdoc />
    public long TotalCachedMemoryBytes => _loadedModels.Values.Sum(m => m.SizeBytes);

    /// <inheritdoc />
    public bool TryGetModel(string name, [NotNullWhen(true)] out ModelInfo? model)
    {
        if (_loadedModels.TryGetValue(name, out model))
        {
            // Update access statistics
            model.LastAccessedAt = _timeProvider.GetUtcNow();
            model.AccessCount++;
            return true;
        }

        model = null;
        return false;
    }

    /// <inheritdoc />
    public async Task<ModelInfo> RegisterModelAsync(
        string name,
        string version,
        ModelType type,
        string path,
        long sizeBytes,
        ModelDevice device = ModelDevice.Cpu,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var now = _timeProvider.GetUtcNow();
        var model = new ModelInfo
        {
            Name = name,
            Version = version,
            Type = type,
            Path = path,
            SizeBytes = sizeBytes,
            Device = device,
            LoadedAt = now,
            LastAccessedAt = now,
            AccessCount = 0,
            IsLoaded = true
        };

        // Check if eviction is needed before adding
        if (_loadedModels.Count >= _options.MaxCachedModels ||
            TotalCachedMemoryBytes + sizeBytes > _options.MaxCacheMemoryBytes)
        {
            await EvictAsync(sizeBytes, cancellationToken).ConfigureAwait(false);
        }

        _loadedModels[name] = model;

        // Track version history
        if (_options.TrackVersions)
        {
            TrackVersion(model);
        }

        _logger.LogInformation(
            "Registered model {Name} v{Version} ({Type}) - {Size:N2} MB on {Device}",
            name, version, type, sizeBytes / (1024.0 * 1024.0), device);

        return model;
    }

    /// <inheritdoc />
    public bool UnloadModel(string name)
    {
        if (_loadedModels.TryRemove(name, out var model))
        {
            _logger.LogInformation(
                "Unloaded model {Name} v{Version} - freed {Size:N2} MB",
                name, model.Version, model.SizeBytes / (1024.0 * 1024.0));
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<ModelInfo> GetLoadedModels()
    {
        return _loadedModels.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<ModelInfo> GetVersionHistory(string name)
    {
        if (_versionHistory.TryGetValue(name, out var history))
        {
            return history.AsReadOnly();
        }

        return Array.Empty<ModelInfo>();
    }

    /// <inheritdoc />
    public async Task<int> EvictAsync(long requiredBytes = 0, CancellationToken cancellationToken = default)
    {
        await _evictionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var evicted = 0;
            var freedBytes = 0L;
            var targetBytes = requiredBytes > 0 ? requiredBytes : _options.MaxCacheMemoryBytes / 4;

            var candidates = GetEvictionCandidates();

            foreach (var candidate in candidates)
            {
                if (freedBytes >= targetBytes && _loadedModels.Count < _options.MaxCachedModels)
                {
                    break;
                }

                if (_loadedModels.TryRemove(candidate.Name, out _))
                {
                    freedBytes += candidate.SizeBytes;
                    evicted++;

                    _logger.LogDebug(
                        "Evicted model {Name} (policy: {Policy}) - freed {Size:N2} MB",
                        candidate.Name, _options.EvictionPolicy, candidate.SizeBytes / (1024.0 * 1024.0));
                }
            }

            if (evicted > 0)
            {
                _logger.LogInformation(
                    "Eviction complete: removed {Count} models, freed {Size:N2} MB",
                    evicted, freedBytes / (1024.0 * 1024.0));
            }

            return evicted;
        }
        finally
        {
            _evictionLock.Release();
        }
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        var count = _loadedModels.Count;
        var size = TotalCachedMemoryBytes;

        _loadedModels.Clear();

        _logger.LogInformation(
            "Cache cleared: removed {Count} models, freed {Size:N2} MB",
            count, size / (1024.0 * 1024.0));
    }

    private IEnumerable<ModelInfo> GetEvictionCandidates()
    {
        return _options.EvictionPolicy switch
        {
            EvictionPolicy.Lru => _loadedModels.Values
                .OrderBy(m => m.LastAccessedAt),

            EvictionPolicy.Lfu => _loadedModels.Values
                .OrderBy(m => m.AccessCount),

            EvictionPolicy.Fifo => _loadedModels.Values
                .OrderBy(m => m.LoadedAt),

            EvictionPolicy.SizeBased => _loadedModels.Values
                .OrderByDescending(m => m.SizeBytes),

            _ => _loadedModels.Values.OrderBy(m => m.LastAccessedAt)
        };
    }

    private void TrackVersion(ModelInfo model)
    {
        var history = _versionHistory.GetOrAdd(model.Name, _ => []);

        lock (history)
        {
            // Remove oldest versions if exceeding limit
            while (history.Count >= _options.MaxVersionsPerModel)
            {
                history.RemoveAt(0);
            }

            history.Add(model with { IsLoaded = false });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _evictionLock.Dispose();
        _loadedModels.Clear();
        _versionHistory.Clear();
    }
}

/// <summary>
/// Interface for the model registry service.
/// </summary>
public interface IModelRegistry
{
    /// <summary>Number of currently loaded models.</summary>
    int LoadedModelCount { get; }

    /// <summary>Total memory used by cached models in bytes.</summary>
    long TotalCachedMemoryBytes { get; }

    /// <summary>Attempts to get a loaded model by name.</summary>
    bool TryGetModel(string name, [NotNullWhen(true)] out ModelInfo? model);

    /// <summary>Registers and caches a new model.</summary>
    Task<ModelInfo> RegisterModelAsync(
        string name,
        string version,
        ModelType type,
        string path,
        long sizeBytes,
        ModelDevice device = ModelDevice.Cpu,
        CancellationToken cancellationToken = default);

    /// <summary>Unloads a model from the cache.</summary>
    bool UnloadModel(string name);

    /// <summary>Gets all currently loaded models.</summary>
    IReadOnlyList<ModelInfo> GetLoadedModels();

    /// <summary>Gets version history for a model.</summary>
    IReadOnlyList<ModelInfo> GetVersionHistory(string name);

    /// <summary>Evicts models based on the configured policy.</summary>
    Task<int> EvictAsync(long requiredBytes = 0, CancellationToken cancellationToken = default);

    /// <summary>Clears all cached models.</summary>
    void ClearCache();
}
