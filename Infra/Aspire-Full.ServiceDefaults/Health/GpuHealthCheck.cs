using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.ServiceDefaults.Health;

/// <summary>
/// Health check for GPU availability and resources.
/// Validates CUDA runtime, VRAM thresholds, and native library accessibility.
/// </summary>
public sealed class GpuHealthCheck : IHealthCheck
{
    private readonly ILogger<GpuHealthCheck> _logger;
    private readonly GpuHealthCheckOptions _options;

    public GpuHealthCheck(ILogger<GpuHealthCheck> logger, GpuHealthCheckOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new GpuHealthCheckOptions();
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if native library is loadable
            if (!IsNativeLibraryAvailable())
            {
                return Task.FromResult(_options.RequireGpu
                    ? HealthCheckResult.Unhealthy("Native tensor library not available")
                    : HealthCheckResult.Degraded("GPU acceleration unavailable - using CPU fallback"));
            }

            // Check GPU device count
            var deviceCount = GetGpuDeviceCount();
            if (deviceCount == 0)
            {
                return Task.FromResult(_options.RequireGpu
                    ? HealthCheckResult.Unhealthy("No GPU devices detected")
                    : HealthCheckResult.Degraded("No GPU devices - using CPU fallback"));
            }

            // Check VRAM availability
            var (totalMemory, freeMemory) = GetGpuMemoryInfo();
            var usedPercent = totalMemory > 0 ? (double)(totalMemory - freeMemory) / totalMemory * 100 : 0;

            var data = new Dictionary<string, object>
            {
                ["gpu_count"] = deviceCount,
                ["total_vram_mb"] = totalMemory / (1024 * 1024),
                ["free_vram_mb"] = freeMemory / (1024 * 1024),
                ["vram_used_percent"] = Math.Round(usedPercent, 1)
            };

            if (usedPercent > _options.CriticalVramThresholdPercent)
            {
                _logger.LogWarning("GPU VRAM usage critical: {UsedPercent}%", usedPercent);
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"GPU VRAM usage critical: {usedPercent:F1}% (threshold: {_options.CriticalVramThresholdPercent}%)",
                    data: data));
            }

            if (usedPercent > _options.WarningVramThresholdPercent)
            {
                _logger.LogInformation("GPU VRAM usage elevated: {UsedPercent}%", usedPercent);
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"GPU VRAM usage elevated: {usedPercent:F1}%",
                    data: data));
            }

            _logger.LogDebug("GPU health check passed: {DeviceCount} device(s), {VramUsed}% VRAM used",
                deviceCount, usedPercent);

            return Task.FromResult(HealthCheckResult.Healthy(
                $"GPU healthy: {deviceCount} device(s), {usedPercent:F1}% VRAM used",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GPU health check failed with exception");
            return Task.FromResult(_options.RequireGpu
                ? HealthCheckResult.Unhealthy("GPU health check failed", ex)
                : HealthCheckResult.Degraded("GPU check failed - using CPU fallback", ex));
        }
    }

    private static bool IsNativeLibraryAvailable()
    {
        try
        {
            // Try to call the initialization function
            var result = NativeGpuInterop.InitTensorContext();
            return result >= 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static int GetGpuDeviceCount()
    {
        try
        {
            return NativeGpuInterop.GetDeviceCount();
        }
        catch
        {
            return 0;
        }
    }

    private static (long total, long free) GetGpuMemoryInfo()
    {
        try
        {
            if (NativeGpuInterop.GetDeviceInfo(0, out var info) == 0)
            {
                return (info.total_memory, info.free_memory);
            }
        }
        catch
        {
            // Ignore
        }
        return (0, 0);
    }
}

/// <summary>
/// Options for GPU health check behavior.
/// </summary>
public sealed class GpuHealthCheckOptions
{
    /// <summary>
    /// If true, GPU must be available for healthy status. If false, CPU fallback is acceptable.
    /// </summary>
    public bool RequireGpu { get; set; } = false;

    /// <summary>
    /// VRAM usage percentage that triggers a warning (degraded status).
    /// </summary>
    public double WarningVramThresholdPercent { get; set; } = 80.0;

    /// <summary>
    /// VRAM usage percentage that triggers critical (unhealthy status).
    /// </summary>
    public double CriticalVramThresholdPercent { get; set; } = 95.0;

    /// <summary>
    /// Minimum required VRAM in MB. 0 means no minimum.
    /// </summary>
    public long MinimumVramMb { get; set; } = 0;
}

/// <summary>
/// Minimal P/Invoke declarations for GPU health checks.
/// Uses DllImport for broad compatibility across all .NET project types.
/// </summary>
internal static class NativeGpuInterop
{
    private const string DllName = "AspireFullNative";

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GpuDeviceInfo
    {
        public int device_id;
        public long total_memory;
        public long free_memory;
        public int compute_capability_major;
        public int compute_capability_minor;
        public int multiprocessor_count;
        public int max_threads_per_block;
        public int warp_size;
        public int max_shared_memory_per_block;
    }

    [System.Runtime.InteropServices.DllImport(DllName, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    public static extern int InitTensorContext();

    [System.Runtime.InteropServices.DllImport(DllName, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    public static extern int GetDeviceCount();

    [System.Runtime.InteropServices.DllImport(DllName, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    public static extern int GetDeviceInfo(int deviceId, out GpuDeviceInfo info);
}
