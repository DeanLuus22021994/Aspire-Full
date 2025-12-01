using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Aspire_Full.DockerRegistry.Configuration;

/// <summary>
/// Configuration options for interacting with a Docker registry using pattern-based repositories and tags.
/// Uses C# 14 field keyword for property validation.
/// </summary>
public sealed class DockerRegistryOptions
{
    private string _baseAddress = "http://localhost:5000/";
    private int _catalogPageSize = 100;
    private TimeSpan _httpTimeout = TimeSpan.FromSeconds(15);
    private int _maxWorkerPoolSize = 5;
    private TimeSpan _garbageCollectionInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// Registry base address. Supports HTTP or HTTPS. Defaults to a local development registry.
    /// </summary>
    [Required]
    public string BaseAddress
    {
        get => _baseAddress;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                throw new ArgumentException("BaseAddress must be a valid HTTP or HTTPS URI", nameof(value));
            }
            _baseAddress = value;
        }
    }

    /// <summary>
    /// Maximum number of repositories to request per catalog page.
    /// </summary>
    [Range(1, 1000)]
    public int CatalogPageSize
    {
        get => _catalogPageSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1000);
            _catalogPageSize = value;
        }
    }

    /// <summary>
    /// Timeout for outgoing HTTP requests.
    /// </summary>
    public TimeSpan HttpTimeout
    {
        get => _httpTimeout;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromMinutes(30));
            _httpTimeout = value;
        }
    }

    /// <summary>
    /// Whether to allow untrusted SSL certificates (development only).
    /// </summary>
    public bool AllowInsecureTls { get; set; }

    /// <summary>
    /// Whether to fall back to pattern-derived sample data when the registry cannot be reached.
    /// </summary>
    public bool EnableOfflineFallback { get; set; } = true;

    /// <summary>
    /// Credential configuration for the registry.
    /// </summary>
    public DockerRegistryCredentialOptions Credentials { get; set; } = new();

    /// <summary>
    /// Pattern configuration for repositories and tags.
    /// </summary>
    public DockerRegistryPatternOptions Patterns { get; set; } = new();

    /// <summary>
    /// Maximum number of concurrent buildx workers.
    /// </summary>
    public int MaxWorkerPoolSize
    {
        get => _maxWorkerPoolSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 32);
            _maxWorkerPoolSize = value;
        }
    }

    /// <summary>
    /// Interval for garbage collection.
    /// </summary>
    public TimeSpan GarbageCollectionInterval
    {
        get => _garbageCollectionInterval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.FromMinutes(5));
            _garbageCollectionInterval = value;
        }
    }

    /// <summary>
    /// GPU acceleration configuration for TensorCore builds.
    /// </summary>
    public GpuAccelerationOptions GpuAcceleration { get; set; } = new();
}

/// <summary>
/// GPU acceleration configuration for NVIDIA CUDA builds.
/// Uses C# 14 field keyword for computed properties and validation.
/// </summary>
public sealed class GpuAccelerationOptions
{
    private bool _enabled = true;
    private string _cudaBootstrapDevelImage = "host.docker.internal:5001/aspire/cuda-bootstrap-devel:latest";
    private string _cudaBootstrapRuntimeImage = "host.docker.internal:5001/aspire/cuda-bootstrap-runtime:latest";
    private string _torchCudaArchList = "7.0 7.5 8.0 8.6 8.9 9.0+PTX";
    private string _minimumCudaVersion = "12.4";
    private string _minimumDriverVersion = "535";
    private int _maxGpuMemoryPoolBuffers = 16;
    private nuint _defaultBufferSize = 64 * 1024 * 1024; // 64MB

    /// <summary>
    /// Whether GPU acceleration is enabled for BuildKit workers.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// The CUDA bootstrap image for devel builds (compilation).
    /// </summary>
    public string CudaBootstrapDevelImage
    {
        get => _cudaBootstrapDevelImage;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _cudaBootstrapDevelImage = value;
        }
    }

    /// <summary>
    /// The CUDA bootstrap image for runtime builds (production).
    /// </summary>
    public string CudaBootstrapRuntimeImage
    {
        get => _cudaBootstrapRuntimeImage;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _cudaBootstrapRuntimeImage = value;
        }
    }

    /// <summary>
    /// Named volume for CUDA compilation cache.
    /// </summary>
    public string CudaCacheVolume { get; set; } = "aspire-cuda-cache";

    /// <summary>
    /// Named volume for ccache (C/C++ compilation cache).
    /// </summary>
    public string CcacheVolume { get; set; } = "aspire-ccache";

    /// <summary>
    /// Named volume for BuildKit cache.
    /// </summary>
    public string BuildkitCacheVolume { get; set; } = "aspire-buildkit-cache";

    /// <summary>
    /// CUDA architectures to target for TensorCore builds.
    /// </summary>
    public string TorchCudaArchList
    {
        get => _torchCudaArchList;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _torchCudaArchList = value;
        }
    }

    /// <summary>
    /// Minimum required CUDA version on the host.
    /// </summary>
    public string MinimumCudaVersion
    {
        get => _minimumCudaVersion;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _minimumCudaVersion = value;
        }
    }

    /// <summary>
    /// Minimum required NVIDIA driver version on the host.
    /// </summary>
    public string MinimumDriverVersion
    {
        get => _minimumDriverVersion;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _minimumDriverVersion = value;
        }
    }

    /// <summary>
    /// Maximum number of GPU memory pool buffers.
    /// </summary>
    public int MaxGpuMemoryPoolBuffers
    {
        get => _maxGpuMemoryPoolBuffers;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 128);
            _maxGpuMemoryPoolBuffers = value;
        }
    }

    /// <summary>
    /// Default buffer size for GPU memory pool allocations.
    /// </summary>
    public nuint DefaultBufferSize
    {
        get => _defaultBufferSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, (nuint)(1024 * 1024)); // 1MB minimum
            _defaultBufferSize = value;
        }
    }

    #region Computed Properties

    /// <summary>
    /// Returns true if high-performance mode is configured (Ampere+ architecture).
    /// </summary>
    public bool IsHighPerformanceMode => _torchCudaArchList.Contains("8.") || _torchCudaArchList.Contains("9.");

    /// <summary>
    /// Returns true if the CUDA toolkit is required (devel image targets).
    /// </summary>
    public bool RequiresCudaToolkit => _cudaBootstrapDevelImage.Contains("devel");

    /// <summary>
    /// Gets the NVIDIA runtime requirement string.
    /// </summary>
    public string NvidiaRequirement => $"cuda>={_minimumCudaVersion},driver>={_minimumDriverVersion}";

    /// <summary>
    /// Gets the total estimated GPU memory requirement in bytes.
    /// </summary>
    public long EstimatedGpuMemoryBytes => (long)_maxGpuMemoryPoolBuffers * (long)_defaultBufferSize;

    #endregion
}

public sealed class DockerRegistryCredentialOptions
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? BearerToken { get; set; }

    public bool HasCredentials => !string.IsNullOrWhiteSpace(BearerToken) || !string.IsNullOrWhiteSpace(Username);
}

public sealed class DockerRegistryPatternOptions
{
    /// <summary>
    /// Template for repository names. Supported tokens: namespace, service, environment, architecture, variant.
    /// </summary>
    [Required]
    public string RepositoryTemplate { get; set; } = "{namespace}/{service}-{environment}";

    /// <summary>
    /// Template for tags. Supported tokens: version, environment, architecture, variant.
    /// </summary>
    [Required]
    public string TagTemplate { get; set; } = "{version}-{architecture}";

    /// <summary>
    /// Default namespace used when formatting repositories.
    /// </summary>
    [Required]
    public string Namespace { get; set; } = "aspire";

    public string DefaultEnvironment { get; set; } = "dev";

    public string DefaultArchitecture { get; set; } = "linux-x64";

    public string DefaultVersion { get; set; } = "1.0.0";

    public string? DefaultVariant { get; set; }

    /// <summary>
    /// A list of services used when sample repositories need to be generated offline.
    /// </summary>
    public IList<string> SampleServices { get; set; } = new List<string> { "api", "workers", "frontend" };
}
