using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace Aspire_Full.Configuration;

// =============================================================================
// Unified Configuration Models for aspire-config.yaml
// =============================================================================

/// <summary>
/// Root configuration model for the unified aspire-config.yaml file.
/// </summary>
public class AspireConfig
{
    [YamlMember(Alias = "version")]
    public int Version { get; set; } = 2;

    [YamlMember(Alias = "repository")]
    public RepositoryConfig Repository { get; set; } = new();

    [YamlMember(Alias = "dotnet")]
    public DotNetConfig DotNet { get; set; } = new();

    [YamlMember(Alias = "python")]
    public PythonConfig Python { get; set; } = new();

    [YamlMember(Alias = "docker")]
    public DockerConfig Docker { get; set; } = new();

    [YamlMember(Alias = "hardware")]
    public HardwareConfig Hardware { get; set; } = new();

    [YamlMember(Alias = "tensor")]
    public TensorConfig Tensor { get; set; } = new();

    [YamlMember(Alias = "agents")]
    public AgentsConfig Agents { get; set; } = new();

    [YamlMember(Alias = "telemetry")]
    public TelemetryConfig Telemetry { get; set; } = new();

    [YamlMember(Alias = "health_checks")]
    public HealthChecksConfig HealthChecks { get; set; } = new();

    [YamlMember(Alias = "models")]
    public ModelsConfig Models { get; set; } = new();

    [YamlMember(Alias = "scaling")]
    public ScalingConfig Scaling { get; set; } = new();
}

public class RepositoryConfig
{
    [YamlMember(Alias = "root")]
    public string Root { get; set; } = ".";

    [YamlMember(Alias = "branch")]
    public string Branch { get; set; } = "master";
}

public class DotNetConfig
{
    [YamlMember(Alias = "sdk")]
    public string Sdk { get; set; } = "10.0.100";

    [YamlMember(Alias = "preview")]
    public bool Preview { get; set; } = true;

    [YamlMember(Alias = "telemetry_optout")]
    public bool TelemetryOptout { get; set; } = true;
}

public class PythonConfig
{
    [YamlMember(Alias = "version")]
    public string Version { get; set; } = "3.15t";

    [YamlMember(Alias = "manager")]
    public string Manager { get; set; } = "uv";

    [YamlMember(Alias = "gil_disabled")]
    public bool GilDisabled { get; set; } = true;

    [YamlMember(Alias = "jit_enabled")]
    public bool JitEnabled { get; set; } = true;

    [YamlMember(Alias = "torch")]
    public TorchConfig Torch { get; set; } = new();
}

public class TorchConfig
{
    [YamlMember(Alias = "device")]
    public string Device { get; set; } = "cuda";

    [YamlMember(Alias = "cuda_arch_list")]
    public string CudaArchList { get; set; } = "7.0 7.5 8.0 8.6 8.9 9.0+PTX";
}

public class DockerConfig
{
    [YamlMember(Alias = "registry")]
    public DockerRegistryConfig Registry { get; set; } = new();

    [YamlMember(Alias = "namespace")]
    public string Namespace { get; set; } = "aspire";

    [YamlMember(Alias = "image_prefix")]
    public string ImagePrefix { get; set; } = "aspire-agents";
}

public class DockerRegistryConfig
{
    [YamlMember(Alias = "host")]
    public string Host { get; set; } = "localhost:5001";

    [YamlMember(Alias = "volume_name")]
    public string VolumeName { get; set; } = "aspire-registry";

    [YamlMember(Alias = "port")]
    public int Port { get; set; } = 5001;

    [YamlMember(Alias = "allow_insecure_tls")]
    public bool AllowInsecureTls { get; set; } = true;
}

public class HardwareConfig
{
    [YamlMember(Alias = "gpu")]
    public GpuHardwareConfig Gpu { get; set; } = new();
}

public class GpuHardwareConfig
{
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = true;

    [YamlMember(Alias = "driver_version")]
    public string DriverVersion { get; set; } = "";

    [YamlMember(Alias = "require_gpu")]
    public bool RequireGpu { get; set; } = false;

    [YamlMember(Alias = "compute_capability")]
    public string ComputeCapability { get; set; } = "8.6";
}

public class TensorConfig
{
    [YamlMember(Alias = "runtime")]
    public TensorRuntimeConfig Runtime { get; set; } = new();

    [YamlMember(Alias = "orchestration")]
    public TensorOrchestrationConfig Orchestration { get; set; } = new();

    [YamlMember(Alias = "batching")]
    public TensorBatchingConfig Batching { get; set; } = new();
}

public class TensorRuntimeConfig
{
    [YamlMember(Alias = "max_buffer_count")]
    public int MaxBufferCount { get; set; } = 32;

    [YamlMember(Alias = "default_buffer_size_mb")]
    public int DefaultBufferSizeMb { get; set; } = 128;

    [YamlMember(Alias = "prefer_gpu")]
    public bool PreferGpu { get; set; } = true;

    [YamlMember(Alias = "enable_metrics")]
    public bool EnableMetrics { get; set; } = true;
}

public class TensorOrchestrationConfig
{
    [YamlMember(Alias = "job_timeout_seconds")]
    public int JobTimeoutSeconds { get; set; } = 300;

    [YamlMember(Alias = "max_concurrent_jobs")]
    public int MaxConcurrentJobs { get; set; } = 16;
}

public class TensorBatchingConfig
{
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = true;

    [YamlMember(Alias = "max_batch_size")]
    public int MaxBatchSize { get; set; } = 32;

    [YamlMember(Alias = "batch_timeout_ms")]
    public int BatchTimeoutMs { get; set; } = 50;

    [YamlMember(Alias = "min_batch_size")]
    public int MinBatchSize { get; set; } = 4;
}

public class AgentsConfig
{
    [YamlMember(Alias = "gpu")]
    public bool Gpu { get; set; } = true;

    [YamlMember(Alias = "replicas")]
    public int Replicas { get; set; } = 1;

    [YamlMember(Alias = "python")]
    public PythonAgentsConfig Python { get; set; } = new();
}

public class PythonAgentsConfig
{
    [YamlMember(Alias = "workers")]
    public int Workers { get; set; } = 4;

    [YamlMember(Alias = "timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 120;
}

public class TelemetryConfig
{
    [YamlMember(Alias = "otlp")]
    public OtlpConfig Otlp { get; set; } = new();

    [YamlMember(Alias = "gpu")]
    public GpuTelemetryConfig Gpu { get; set; } = new();
}

public class OtlpConfig
{
    [YamlMember(Alias = "endpoint")]
    public string Endpoint { get; set; } = "http://aspire-dashboard:18889";

    [YamlMember(Alias = "protocol")]
    public string Protocol { get; set; } = "http/protobuf";
}

public class GpuTelemetryConfig
{
    [YamlMember(Alias = "snapshot_interval_seconds")]
    public int SnapshotIntervalSeconds { get; set; } = 5;

    [YamlMember(Alias = "target_utilization")]
    public int TargetUtilization { get; set; } = 80;

    [YamlMember(Alias = "warning_threshold")]
    public int WarningThreshold { get; set; } = 70;

    [YamlMember(Alias = "critical_threshold")]
    public int CriticalThreshold { get; set; } = 95;

    // Legacy support for old config format
    [YamlMember(Alias = "snapshot")]
    public GpuSnapshot? Snapshot { get; set; }
}

public class GpuSnapshot
{
    [YamlMember(Alias = "target_utilization")]
    public double TargetUtilization { get; set; }
}

public class HealthChecksConfig
{
    [YamlMember(Alias = "gpu")]
    public GpuHealthChecksConfig Gpu { get; set; } = new();
}

public class GpuHealthChecksConfig
{
    [YamlMember(Alias = "require_gpu")]
    public bool RequireGpu { get; set; } = false;

    [YamlMember(Alias = "warning_vram_threshold_percent")]
    public double WarningVramThresholdPercent { get; set; } = 80.0;

    [YamlMember(Alias = "critical_vram_threshold_percent")]
    public double CriticalVramThresholdPercent { get; set; } = 95.0;

    [YamlMember(Alias = "minimum_vram_mb")]
    public long MinimumVramMb { get; set; } = 0;
}

public class ModelsConfig
{
    [YamlMember(Alias = "cache_directory")]
    public string CacheDirectory { get; set; } = "/models";

    [YamlMember(Alias = "preload")]
    public List<ModelPreloadConfig> Preload { get; set; } = [];

    [YamlMember(Alias = "registry")]
    public ModelRegistryConfig Registry { get; set; } = new();
}

public class ModelPreloadConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "embedding";

    [YamlMember(Alias = "priority")]
    public string Priority { get; set; } = "normal";
}

public class ModelRegistryConfig
{
    [YamlMember(Alias = "track_versions")]
    public bool TrackVersions { get; set; } = true;

    [YamlMember(Alias = "max_cached_models")]
    public int MaxCachedModels { get; set; } = 10;

    [YamlMember(Alias = "eviction_policy")]
    public string EvictionPolicy { get; set; } = "lru";
}

public class ScalingConfig
{
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = false;

    [YamlMember(Alias = "min_replicas")]
    public int MinReplicas { get; set; } = 1;

    [YamlMember(Alias = "max_replicas")]
    public int MaxReplicas { get; set; } = 4;

    [YamlMember(Alias = "target_gpu_utilization")]
    public int TargetGpuUtilization { get; set; } = 70;

    [YamlMember(Alias = "scale_up_threshold")]
    public int ScaleUpThreshold { get; set; } = 80;

    [YamlMember(Alias = "scale_down_threshold")]
    public int ScaleDownThreshold { get; set; } = 30;

    [YamlMember(Alias = "cooldown_seconds")]
    public int CooldownSeconds { get; set; } = 60;
}

// =============================================================================
// Legacy RuntimeConfig for backward compatibility
// =============================================================================
public class RuntimeConfig
{
    [YamlMember(Alias = "telemetry")]
    public TelemetryConfig Telemetry { get; set; } = new();

    [YamlMember(Alias = "environment")]
    public EnvironmentConfig Environment { get; set; } = new();
}

public class EnvironmentConfig
{
    [YamlMember(Alias = "dotnet")]
    public DotNetConfig DotNet { get; set; } = new();

    [YamlMember(Alias = "python")]
    public PythonConfig Python { get; set; } = new();

    [YamlMember(Alias = "hardware")]
    public HardwareConfig Hardware { get; set; } = new();
}
