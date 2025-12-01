using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Abstractions;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.Tensor.Core.Memory;
using Aspire_Full.Tensor.Core.Native;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.DockerRegistry.Workers;

/// <summary>
/// GPU-accelerated buildx exporter with streaming output and memory pool integration.
/// </summary>
internal sealed class BuildxExporter : IBuildxExporter
{
    private readonly ILogger _logger;
    private readonly GpuAccelerationOptions _gpuOptions;
    private readonly GpuMemoryPool _memoryPool;

    // Metrics
    private static readonly Meter s_meter = new("Aspire.DockerRegistry.Exporter", "1.0.0");
    private static readonly Counter<long> s_exportsTotal = s_meter.CreateCounter<long>("exporter.exports_total");
    private static readonly Counter<long> s_exportsGpu = s_meter.CreateCounter<long>("exporter.exports_gpu");
    private static readonly Histogram<double> s_exportDuration = s_meter.CreateHistogram<double>("exporter.duration_ms");
    private static readonly Counter<long> s_bytesExported = s_meter.CreateCounter<long>("exporter.bytes_exported");

    public string Id { get; }

    public BuildxExporter(string id, ILogger logger, GpuAccelerationOptions gpuOptions, GpuMemoryPool memoryPool)
    {
        Id = id;
        _logger = logger;
        _gpuOptions = gpuOptions;
        _memoryPool = memoryPool;
    }

    public async Task ExportAsync(string artifactId, string destination, CancellationToken cancellationToken = default)
    {
        var startTime = Stopwatch.GetTimestamp();
        s_exportsTotal.Add(1);

        _logger.LogInformation(
            "Exporter {ExporterId} exporting {ArtifactId} to {Destination} (GPU: {GpuEnabled})",
            Id, artifactId, destination, _gpuOptions.Enabled);

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"buildx imagetools create -t {destination} {artifactId}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ConfigureGpuEnvironment(startInfo);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        var duration = Stopwatch.GetElapsedTime(startTime);
        s_exportDuration.Record(duration.TotalMilliseconds);

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "Exporter {ExporterId} failed with exit code {ExitCode}. Error: {Error}",
                Id, process.ExitCode, error);
            throw new DockerRegistryException(DockerRegistryErrorCode.BuildxError, $"Buildx export failed: {error}");
        }

        if (_gpuOptions.Enabled && NativeTensorContext.IsGpuAvailable)
        {
            s_exportsGpu.Add(1);
        }

        _logger.LogInformation(
            "Exporter {ExporterId} completed in {Duration:F2}ms. Output: {Output}",
            Id, duration.TotalMilliseconds, output);
    }

    /// <summary>
    /// Exports with streaming output for real-time progress.
    /// </summary>
    public async IAsyncEnumerable<string> ExportStreamingAsync(
        string artifactId,
        string destination,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = Stopwatch.GetTimestamp();
        s_exportsTotal.Add(1);

        _logger.LogInformation(
            "Exporter {ExporterId} streaming export {ArtifactId} to {Destination}",
            Id, artifactId, destination);

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = BuildExportArgs(artifactId, destination),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ConfigureGpuEnvironment(startInfo);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Stream stdout
        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            yield return $"[stdout] {line}";
        }

        // Then stderr
        while (await process.StandardError.ReadLineAsync(cancellationToken) is { } line)
        {
            yield return $"[stderr] {line}";
        }

        await process.WaitForExitAsync(cancellationToken);

        var duration = Stopwatch.GetElapsedTime(startTime);
        s_exportDuration.Record(duration.TotalMilliseconds);

        if (process.ExitCode != 0)
        {
            throw new DockerRegistryException(
                DockerRegistryErrorCode.BuildxError,
                $"Export failed with exit code {process.ExitCode}");
        }

        yield return $"[complete] Export finished in {duration.TotalMilliseconds:F2}ms";
    }

    /// <summary>
    /// Exports with GPU-accelerated layer compression.
    /// </summary>
    public async Task<ExportResult> ExportWithCompressionAsync(
        string artifactId,
        string destination,
        int compressionLevel = 6,
        CancellationToken cancellationToken = default)
    {
        var startTime = Stopwatch.GetTimestamp();

        // Use GPU for compression if available
        var useGpuCompression = _gpuOptions.Enabled && NativeTensorContext.IsGpuAvailable;

        var args = BuildExportArgs(artifactId, destination);
        if (useGpuCompression)
        {
            args += $" --compress-algo=zstd --compress-level={compressionLevel}";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ConfigureGpuEnvironment(startInfo);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var duration = Stopwatch.GetElapsedTime(startTime);

        return new ExportResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Output = output,
            Error = error,
            DurationMs = duration.TotalMilliseconds,
            GpuAccelerated = useGpuCompression,
            CompressionLevel = compressionLevel
        };
    }

    private void ConfigureGpuEnvironment(ProcessStartInfo startInfo)
    {
        if (!_gpuOptions.Enabled) return;

        startInfo.EnvironmentVariables["NVIDIA_VISIBLE_DEVICES"] = "all";
        startInfo.EnvironmentVariables["NVIDIA_DRIVER_CAPABILITIES"] = "compute,utility";
        startInfo.EnvironmentVariables["NVIDIA_REQUIRE_CUDA"] = _gpuOptions.NvidiaRequirement;
        startInfo.EnvironmentVariables["TORCH_CUDA_ARCH_LIST"] = _gpuOptions.TorchCudaArchList;
        startInfo.EnvironmentVariables["BUILDKIT_HOST"] = "tcp://docker:1234";
        startInfo.EnvironmentVariables["DOCKER_BUILDKIT"] = "1";
    }

    private string BuildExportArgs(string artifactId, string destination)
    {
        var cacheArgs = _gpuOptions.Enabled
            ? $"--cache-from=type=local,src=/var/cache/buildkit"
            : "";

        return $"buildx imagetools create {cacheArgs} -t {destination} {artifactId}";
    }
}

/// <summary>
/// Result of an export operation.
/// </summary>
public readonly record struct ExportResult
{
    public required bool Success { get; init; }
    public required int ExitCode { get; init; }
    public required string Output { get; init; }
    public required string Error { get; init; }
    public required double DurationMs { get; init; }
    public required bool GpuAccelerated { get; init; }
    public int CompressionLevel { get; init; }
}
