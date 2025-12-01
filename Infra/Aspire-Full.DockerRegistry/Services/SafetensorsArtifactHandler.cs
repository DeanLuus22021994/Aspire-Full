using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Aspire_Full.Tensor.Core.Memory;
using Aspire_Full.Tensor.Core.Native;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.DockerRegistry.Services;

/// <summary>
/// High-performance safetensors artifact handler with zero-copy GPU processing.
/// Uses memory-mapped files and IAsyncEnumerable for streaming large model files.
/// </summary>
public sealed class SafetensorsArtifactHandler : IAsyncDisposable
{
    private readonly ILogger<SafetensorsArtifactHandler> _logger;
    private readonly GpuMemoryPool _memoryPool;
    private readonly Channel<TensorChunk> _chunkChannel;
    private bool _disposed;

    // Metrics
    private static readonly Meter s_meter = new("Aspire.DockerRegistry.Safetensors", "1.0.0");
    private static readonly Counter<long> s_bytesProcessed = s_meter.CreateCounter<long>("safetensors.bytes_processed");
    private static readonly Counter<long> s_chunksProcessed = s_meter.CreateCounter<long>("safetensors.chunks_processed");
    private static readonly Histogram<double> s_chunkDuration = s_meter.CreateHistogram<double>("safetensors.chunk_duration_ms");
    private static readonly Histogram<double> s_throughput = s_meter.CreateHistogram<double>("safetensors.throughput_mbps");

    public SafetensorsArtifactHandler(
        ILogger<SafetensorsArtifactHandler> logger,
        GpuMemoryPool memoryPool)
    {
        _logger = logger;
        _memoryPool = memoryPool;
        _chunkChannel = Channel.CreateBounded<TensorChunk>(new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
    }

    /// <summary>
    /// Channel reader for streaming tensor chunks during processing.
    /// </summary>
    public ChannelReader<TensorChunk> ChunkReader => _chunkChannel.Reader;

    /// <summary>
    /// Processes a safetensors file using memory-mapped I/O for zero-copy GPU access.
    /// </summary>
    public async Task<SafetensorsResult> ProcessFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Safetensors file not found", filePath);
        }

        var startTime = Stopwatch.GetTimestamp();
        _logger.LogInformation("Processing safetensors file: {FilePath} ({Size:N0} bytes)", filePath, fileInfo.Length);

        using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);

        var result = await ProcessMemoryMappedFileAsync(mmf, fileInfo.Length, cancellationToken);

        var duration = Stopwatch.GetElapsedTime(startTime);
        var throughputMbps = (fileInfo.Length / (1024.0 * 1024.0)) / duration.TotalSeconds;

        s_throughput.Record(throughputMbps);
        s_bytesProcessed.Add(fileInfo.Length);

        _logger.LogInformation(
            "Processed safetensors file in {Duration:F2}ms ({Throughput:F2} MB/s, GPU: {GpuUsed})",
            duration.TotalMilliseconds, throughputMbps, result.GpuAccelerated);

        return result with
        {
            DurationMs = duration.TotalMilliseconds,
            ThroughputMbps = throughputMbps
        };
    }

    /// <summary>
    /// Streams a safetensors file as tensor chunks for incremental processing.
    /// </summary>
    public async IAsyncEnumerable<TensorChunk> StreamChunksAsync(
        string filePath,
        nuint chunkSize = 16 * 1024 * 1024, // 16MB chunks
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Safetensors file not found", filePath);
        }

        using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);

        long offset = 0;
        int chunkIndex = 0;

        while (offset < fileInfo.Length)
        {
            var remainingBytes = fileInfo.Length - offset;
            var currentChunkSize = (long)Math.Min((long)chunkSize, remainingBytes);

            using var accessor = mmf.CreateViewAccessor(offset, currentChunkSize, MemoryMappedFileAccess.Read);

            var chunk = await ProcessChunkAsync(accessor, offset, currentChunkSize, chunkIndex, cancellationToken);

            s_chunksProcessed.Add(1);

            yield return chunk;

            offset += currentChunkSize;
            chunkIndex++;
        }
    }

    /// <summary>
    /// Processes a stream using zero-copy pipeline for GPU offload.
    /// </summary>
    public async Task ProcessStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(
            bufferSize: 64 * 1024,
            minimumReadSize: 16 * 1024,
            leaveOpen: true
        ));

        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                if (buffer.Length > 0)
                {
                    await ProcessBufferWithGpuAsync(buffer, cancellationToken);
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    /// <summary>
    /// Validates tensor data integrity using GPU-accelerated hashing.
    /// </summary>
    public async Task<TensorValidationInfo> ValidateTensorDataAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var startTime = Stopwatch.GetTimestamp();

        if (NativeTensorContext.IsGpuAvailable)
        {
            return await ValidateWithGpuAsync(data, cancellationToken);
        }

        return ValidateWithCpu(data.Span);
    }

    private async Task<SafetensorsResult> ProcessMemoryMappedFileAsync(
        MemoryMappedFile mmf,
        long fileSize,
        CancellationToken cancellationToken)
    {
        using var accessor = mmf.CreateViewAccessor(0, fileSize, MemoryMappedFileAccess.Read);

        unsafe
        {
            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            try
            {
                var dataSpan = new ReadOnlySpan<byte>(ptr, (int)Math.Min(fileSize, int.MaxValue));

                if (NativeTensorContext.IsGpuAvailable)
                {
                    var metrics = new NativeTensorContext.TensorMetrics();
                    var hashBuffer = new byte[32];

                    NativeTensorContext.HashTensorContent((nint)ptr, (nuint)fileSize, hashBuffer, ref metrics);

                    return new SafetensorsResult
                    {
                        IsValid = true,
                        Hash = Convert.ToHexString(hashBuffer),
                        Size = fileSize,
                        GpuAccelerated = true,
                        GpuComputeTimeMs = metrics.compute_time_ms,
                        GpuMemoryUsageMb = metrics.memory_usage_mb
                    };
                }
                else
                {
                    // CPU fallback with XxHash128
                    var hash = System.IO.Hashing.XxHash128.Hash(dataSpan);

                    return new SafetensorsResult
                    {
                        IsValid = true,
                        Hash = Convert.ToHexString(hash),
                        Size = fileSize,
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

    private Task<TensorChunk> ProcessChunkAsync(
        MemoryMappedViewAccessor accessor,
        long offset,
        long size,
        int index,
        CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.GetTimestamp();

        // Process synchronously inside unsafe block, return Task wrapper
        var result = ProcessChunkSync(accessor, offset, size, index, startTime);
        return Task.FromResult(result);
    }

    private TensorChunk ProcessChunkSync(
        MemoryMappedViewAccessor accessor,
        long offset,
        long size,
        int index,
        long startTime)
    {
        unsafe
        {
            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            try
            {
                var dataSpan = new ReadOnlySpan<byte>(ptr, (int)size);

                string hash;
                bool gpuUsed = false;

                if (NativeTensorContext.IsGpuAvailable)
                {
                    var metrics = new NativeTensorContext.TensorMetrics();
                    var hashBuffer = new byte[32];
                    NativeTensorContext.HashTensorContent((nint)ptr, (nuint)size, hashBuffer, ref metrics);
                    hash = Convert.ToHexString(hashBuffer);
                    gpuUsed = true;
                }
                else
                {
                    var hashBytes = System.IO.Hashing.XxHash128.Hash(dataSpan);
                    hash = Convert.ToHexString(hashBytes);
                }

                var duration = Stopwatch.GetElapsedTime(startTime);
                s_chunkDuration.Record(duration.TotalMilliseconds);

                return new TensorChunk
                {
                    Index = index,
                    Offset = offset,
                    Size = size,
                    Hash = hash,
                    GpuAccelerated = gpuUsed,
                    ProcessingTimeMs = duration.TotalMilliseconds
                };
            }
            finally
            {
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }
    }

    private Task ProcessBufferWithGpuAsync(
        ReadOnlySequence<byte> buffer,
        CancellationToken cancellationToken)
    {
        if (buffer.IsSingleSegment)
        {
            ProcessSingleSegmentSync(buffer.FirstSpan);
        }
        else
        {
            // For multi-segment buffers, copy to contiguous memory from pool
            var gpuBuffer = _memoryPool.Rent((nuint)buffer.Length);
            try
            {
                buffer.CopyTo(gpuBuffer.AsSpan());
                ProcessSingleSegmentSync(gpuBuffer.AsSpan().Slice(0, (int)buffer.Length));
            }
            finally
            {
                _memoryPool.Return(gpuBuffer);
            }
        }

        return Task.CompletedTask;
    }

    private void ProcessSingleSegmentSync(ReadOnlySpan<byte> segment)
    {
        s_bytesProcessed.Add(segment.Length);

        if (NativeTensorContext.IsGpuAvailable && segment.Length >= 4096)
        {
            // Only use GPU for larger segments
            unsafe
            {
                fixed (byte* ptr = segment)
                {
                    var metrics = new NativeTensorContext.TensorMetrics();
                    var hashBuffer = new byte[32];
                    NativeTensorContext.HashTensorContent((nint)ptr, (nuint)segment.Length, hashBuffer, ref metrics);

                    _logger.LogDebug(
                        "GPU processed {Size} bytes in {Time:F2}ms",
                        segment.Length, metrics.compute_time_ms);
                }
            }
        }
        else
        {
            // CPU fallback
            _ = System.IO.Hashing.XxHash128.Hash(segment);
            _logger.LogDebug("CPU processed {Size} bytes", segment.Length);
        }
    }

    private Task<TensorValidationInfo> ValidateWithGpuAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.GetTimestamp();
        TensorValidationInfo result;

        unsafe
        {
            using var handle = data.Pin();
            var ptr = (byte*)handle.Pointer;

            var metrics = new NativeTensorContext.TensorMetrics();
            var hashBuffer = new byte[32];

            NativeTensorContext.HashTensorContent((nint)ptr, (nuint)data.Length, hashBuffer, ref metrics);

            result = new TensorValidationInfo
            {
                IsValid = true,
                Hash = Convert.ToHexString(hashBuffer),
                GpuAccelerated = true,
                ComputeTimeMs = metrics.compute_time_ms,
                Size = data.Length
            };
        }

        return Task.FromResult(result);
    }

    private TensorValidationInfo ValidateWithCpu(ReadOnlySpan<byte> data)
    {
        var startTime = Stopwatch.GetTimestamp();
        var hash = System.IO.Hashing.XxHash128.Hash(data);
        var duration = Stopwatch.GetElapsedTime(startTime);

        return new TensorValidationInfo
        {
            IsValid = true,
            Hash = Convert.ToHexString(hash),
            GpuAccelerated = false,
            ComputeTimeMs = (float)duration.TotalMilliseconds,
            Size = data.Length
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _chunkChannel.Writer.Complete();
        await Task.CompletedTask;
    }
}

#region Result Types

public readonly record struct SafetensorsResult
{
    public required bool IsValid { get; init; }
    public required string Hash { get; init; }
    public required long Size { get; init; }
    public required bool GpuAccelerated { get; init; }
    public double DurationMs { get; init; }
    public double ThroughputMbps { get; init; }
    public float GpuComputeTimeMs { get; init; }
    public float GpuMemoryUsageMb { get; init; }
}

public readonly record struct TensorChunk
{
    public required int Index { get; init; }
    public required long Offset { get; init; }
    public required long Size { get; init; }
    public required string Hash { get; init; }
    public required bool GpuAccelerated { get; init; }
    public double ProcessingTimeMs { get; init; }
}

public readonly record struct TensorValidationInfo
{
    public required bool IsValid { get; init; }
    public required string Hash { get; init; }
    public required bool GpuAccelerated { get; init; }
    public float ComputeTimeMs { get; init; }
    public int Size { get; init; }
}

#endregion
