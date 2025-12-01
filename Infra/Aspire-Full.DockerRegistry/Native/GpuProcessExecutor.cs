using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.Tensor.Core.Memory;
using Aspire_Full.Tensor.Core.Native;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.DockerRegistry.Native;

/// <summary>
/// GPU-accelerated process executor with real-time streaming and memory-mapped I/O.
/// Provides IAsyncEnumerable for streaming build output with GPU telemetry.
/// </summary>
public sealed class GpuProcessExecutor : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly GpuAccelerationOptions _gpuOptions;
    private readonly GpuMemoryPool _memoryPool;
    private readonly Channel<GpuTelemetryEvent> _telemetryChannel;
    private bool _disposed;

    // Metrics
    private static readonly Meter s_meter = new("Aspire.DockerRegistry.Process", "1.0.0");
    private static readonly Counter<long> s_processesStarted = s_meter.CreateCounter<long>("process.started");
    private static readonly Counter<long> s_processesCompleted = s_meter.CreateCounter<long>("process.completed");
    private static readonly Histogram<double> s_processDuration = s_meter.CreateHistogram<double>("process.duration_ms");
    private static readonly Counter<long> s_bytesProcessed = s_meter.CreateCounter<long>("process.bytes_processed");

    public GpuProcessExecutor(
        ILogger logger,
        GpuAccelerationOptions gpuOptions,
        GpuMemoryPool memoryPool)
    {
        _logger = logger;
        _gpuOptions = gpuOptions;
        _memoryPool = memoryPool;
        _telemetryChannel = Channel.CreateUnbounded<GpuTelemetryEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Channel reader for GPU telemetry events.
    /// </summary>
    public ChannelReader<GpuTelemetryEvent> TelemetryEvents => _telemetryChannel.Reader;

    /// <summary>
    /// Executes a command with real-time output streaming.
    /// </summary>
    public async IAsyncEnumerable<string> ExecuteStreamingAsync(
        string command,
        string arguments,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var startTime = Stopwatch.GetTimestamp();
        s_processesStarted.Add(1);

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ConfigureGpuEnvironment(startInfo);

        _logger.LogInformation("Starting process: {Command} {Arguments} (GPU: {GpuEnabled})",
            command, arguments, _gpuOptions.Enabled);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputChannel = Channel.CreateUnbounded<string>();

        // Stream stdout and stderr concurrently
        var stdoutTask = StreamOutputAsync(process.StandardOutput, outputChannel.Writer, "stdout", cancellationToken);
        var stderrTask = StreamOutputAsync(process.StandardError, outputChannel.Writer, "stderr", cancellationToken);

        // Yield lines as they arrive
        await foreach (var line in outputChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return line;
        }

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        var duration = Stopwatch.GetElapsedTime(startTime);
        s_processDuration.Record(duration.TotalMilliseconds);
        s_processesCompleted.Add(1);

        await _telemetryChannel.Writer.WriteAsync(new GpuTelemetryEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = GpuTelemetryEventType.ProcessCompleted,
            ProcessId = process.Id,
            ExitCode = process.ExitCode,
            DurationMs = duration.TotalMilliseconds,
            GpuEnabled = _gpuOptions.Enabled
        }, cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.LogError("Process exited with code {ExitCode}", process.ExitCode);
        }
    }

    /// <summary>
    /// Executes a buildx command with GPU passthrough and memory-mapped artifact handling.
    /// </summary>
    public async Task<GpuProcessResult> ExecuteBuildxAsync(
        string subCommand,
        string dockerfile,
        string context,
        string tag,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var startTime = Stopwatch.GetTimestamp();

        // Build the command with GPU optimization flags
        var args = BuildGpuBuildxArgs(subCommand, dockerfile, context, tag);

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

        // Use memory pool for large output buffering
        using var outputBuffer = new MemoryStream();
        using var errorBuffer = new MemoryStream();

        var outputTask = CopyToBufferWithPoolAsync(process.StandardOutput.BaseStream, outputBuffer, cancellationToken);
        var errorTask = CopyToBufferWithPoolAsync(process.StandardError.BaseStream, errorBuffer, cancellationToken);

        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync(cancellationToken);

        var duration = Stopwatch.GetElapsedTime(startTime);

        outputBuffer.Position = 0;
        errorBuffer.Position = 0;

        using var outputReader = new StreamReader(outputBuffer);
        using var errorReader = new StreamReader(errorBuffer);

        return new GpuProcessResult
        {
            ExitCode = process.ExitCode,
            Output = await outputReader.ReadToEndAsync(cancellationToken),
            Error = await errorReader.ReadToEndAsync(cancellationToken),
            DurationMs = duration.TotalMilliseconds,
            GpuUtilized = _gpuOptions.Enabled && NativeTensorContext.IsGpuAvailable
        };
    }

    /// <summary>
    /// Processes a large artifact file using memory-mapped I/O for zero-copy GPU access.
    /// </summary>
    public async Task<ArtifactProcessResult> ProcessArtifactWithGpuAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Artifact not found", filePath);
        }

        var startTime = Stopwatch.GetTimestamp();
        s_bytesProcessed.Add(fileInfo.Length);

        // Process synchronously with GPU, then send telemetry async
        var result = ProcessArtifactSync(filePath, fileInfo.Length, startTime);

        // Send telemetry outside unsafe context
        if (result.GpuAccelerated)
        {
            await _telemetryChannel.Writer.WriteAsync(new GpuTelemetryEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = GpuTelemetryEventType.ArtifactProcessed,
                BytesProcessed = fileInfo.Length,
                DurationMs = result.DurationMs,
                GpuEnabled = true,
                GpuComputeTimeMs = result.GpuMetrics.compute_time_ms,
                GpuMemoryUsageMb = result.GpuMetrics.memory_usage_mb
            }, cancellationToken);
        }

        return result;
    }

    private ArtifactProcessResult ProcessArtifactSync(string filePath, long fileLength, long startTime)
    {
        using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        using var accessor = mmf.CreateViewAccessor(0, fileLength, MemoryMappedFileAccess.Read);

        unsafe
        {
            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            try
            {
                var dataSpan = new ReadOnlySpan<byte>(ptr, (int)fileLength);

                if (NativeTensorContext.IsGpuAvailable)
                {
                    var metrics = new NativeTensorContext.TensorMetrics();
                    var hashBuffer = new byte[32];

                    NativeTensorContext.HashTensorContent(
                        (nint)ptr,
                        (nuint)fileLength,
                        hashBuffer,
                        ref metrics);

                    return new ArtifactProcessResult
                    {
                        Hash = Convert.ToHexString(hashBuffer),
                        Size = fileLength,
                        DurationMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds,
                        GpuAccelerated = true,
                        GpuMetrics = metrics
                    };
                }
                else
                {
                    var hash = System.IO.Hashing.XxHash128.Hash(dataSpan);

                    return new ArtifactProcessResult
                    {
                        Hash = Convert.ToHexString(hash),
                        Size = fileLength,
                        DurationMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds,
                        GpuAccelerated = false
                    };
                }
            }
            finally
            {
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }
    }

    private void ConfigureGpuEnvironment(ProcessStartInfo startInfo)
    {
        if (!_gpuOptions.Enabled) return;

        startInfo.EnvironmentVariables["NVIDIA_VISIBLE_DEVICES"] = "all";
        startInfo.EnvironmentVariables["NVIDIA_DRIVER_CAPABILITIES"] = "compute,utility";
        startInfo.EnvironmentVariables["NVIDIA_REQUIRE_CUDA"] = $"cuda>={_gpuOptions.MinimumCudaVersion},driver>={_gpuOptions.MinimumDriverVersion}";
        startInfo.EnvironmentVariables["TORCH_CUDA_ARCH_LIST"] = _gpuOptions.TorchCudaArchList;
        startInfo.EnvironmentVariables["CUDA_CACHE_PATH"] = "/var/cache/cuda";
        startInfo.EnvironmentVariables["CCACHE_DIR"] = "/root/.ccache";
        startInfo.EnvironmentVariables["BUILDKIT_HOST"] = "tcp://docker:1234";
        startInfo.EnvironmentVariables["DOCKER_BUILDKIT"] = "1";
    }

    private string BuildGpuBuildxArgs(string subCommand, string dockerfile, string context, string tag)
    {
        var cacheArgs = _gpuOptions.Enabled
            ? $"--cache-from=type=local,src=/var/cache/buildkit --cache-to=type=local,dest=/var/cache/buildkit,mode=max"
            : "";

        var buildArgs = _gpuOptions.Enabled
            ? $"--build-arg CUDA_BOOTSTRAP_IMAGE={_gpuOptions.CudaBootstrapDevelImage} " +
              $"--build-arg TORCH_CUDA_ARCH_LIST=\"{_gpuOptions.TorchCudaArchList}\""
            : "";

        return $"buildx {subCommand} {cacheArgs} {buildArgs} -f {dockerfile} -t {tag} {context}";
    }

    private static async Task StreamOutputAsync(
        StreamReader reader,
        ChannelWriter<string> writer,
        string streamName,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                await writer.WriteAsync($"[{streamName}] {line}", cancellationToken);
            }
        }
        finally
        {
            if (streamName == "stderr")
            {
                writer.Complete();
            }
        }
    }

    private static async Task CopyToBufferWithPoolAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 64 * 1024; // 64KB chunks
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _telemetryChannel.Writer.Complete();
        await Task.CompletedTask;
    }
}

#region Result Types

public readonly record struct GpuProcessResult
{
    public required int ExitCode { get; init; }
    public required string Output { get; init; }
    public required string Error { get; init; }
    public required double DurationMs { get; init; }
    public required bool GpuUtilized { get; init; }
}

public readonly record struct ArtifactProcessResult
{
    public required string Hash { get; init; }
    public required long Size { get; init; }
    public required double DurationMs { get; init; }
    public required bool GpuAccelerated { get; init; }
    public NativeTensorContext.TensorMetrics GpuMetrics { get; init; }
}

public enum GpuTelemetryEventType
{
    ProcessStarted,
    ProcessCompleted,
    ArtifactProcessed,
    GpuKernelExecuted,
    MemoryAllocated,
    MemoryFreed
}

public readonly record struct GpuTelemetryEvent
{
    public required DateTime Timestamp { get; init; }
    public required GpuTelemetryEventType EventType { get; init; }
    public int ProcessId { get; init; }
    public int ExitCode { get; init; }
    public long BytesProcessed { get; init; }
    public double DurationMs { get; init; }
    public bool GpuEnabled { get; init; }
    public float GpuComputeTimeMs { get; init; }
    public float GpuMemoryUsageMb { get; init; }
}

#endregion
