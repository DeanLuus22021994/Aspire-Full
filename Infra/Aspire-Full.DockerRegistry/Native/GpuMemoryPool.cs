using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Aspire_Full.DockerRegistry.Native;

/// <summary>
/// High-performance GPU memory pool with pre-allocated device buffers.
/// Uses C# 14 features and .NET 10 TensorPrimitives for portable compute.
/// </summary>
public sealed class GpuMemoryPool : IDisposable
{
    private readonly ConcurrentQueue<GpuBuffer> _availableBuffers = new();
    private readonly ConcurrentDictionary<nint, GpuBuffer> _allocatedBuffers = new();
    private readonly SemaphoreSlim _allocationLock = new(1, 1);
    private readonly int _maxBufferCount;
    private readonly nuint _defaultBufferSize;
    private int _totalAllocated;
    private long _totalBytesAllocated;
    private bool _disposed;

    // Metrics for telemetry
    private static readonly Meter s_meter = new("Aspire.DockerRegistry.Gpu", "1.0.0");
    private static readonly Counter<long> s_allocationsCounter = s_meter.CreateCounter<long>("gpu.memory.allocations");
    private static readonly Histogram<double> s_allocationDuration = s_meter.CreateHistogram<double>("gpu.memory.allocation_duration_ms");
    private static readonly UpDownCounter<long> s_activeBuffers = s_meter.CreateUpDownCounter<long>("gpu.memory.active_buffers");
    private static readonly ObservableGauge<long> s_totalMemory;

    private static long s_globalTotalBytes;

    static GpuMemoryPool()
    {
        s_totalMemory = s_meter.CreateObservableGauge("gpu.memory.total_bytes", () => Interlocked.Read(ref s_globalTotalBytes));
    }

    public GpuMemoryPool(int maxBufferCount = 16, nuint defaultBufferSize = 64 * 1024 * 1024) // 64MB default
    {
        _maxBufferCount = maxBufferCount;
        _defaultBufferSize = defaultBufferSize;
    }

    /// <summary>
    /// Total number of allocated GPU buffers.
    /// </summary>
    public int TotalAllocated => _totalAllocated;

    /// <summary>
    /// Total bytes allocated across all GPU buffers.
    /// </summary>
    public long TotalBytesAllocated => Interlocked.Read(ref _totalBytesAllocated);

    /// <summary>
    /// Number of available buffers in the pool.
    /// </summary>
    public int AvailableCount => _availableBuffers.Count;

    /// <summary>
    /// Rents a GPU buffer from the pool, allocating if necessary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GpuBuffer Rent(nuint minimumSize = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var requiredSize = minimumSize == 0 ? _defaultBufferSize : minimumSize;

        // Try to get an existing buffer of sufficient size
        if (_availableBuffers.TryDequeue(out var buffer) && buffer.Size >= requiredSize)
        {
            _allocatedBuffers.TryAdd(buffer.DevicePointer, buffer);
            s_activeBuffers.Add(1);
            return buffer;
        }

        // Need to allocate a new buffer
        return AllocateNewBuffer(requiredSize);
    }

    /// <summary>
    /// Returns a GPU buffer to the pool for reuse.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(GpuBuffer buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_allocatedBuffers.TryRemove(buffer.DevicePointer, out _))
        {
            buffer.Reset();
            _availableBuffers.Enqueue(buffer);
            s_activeBuffers.Add(-1);
        }
    }

    /// <summary>
    /// Executes a batch of GPU operations using pooled memory.
    /// </summary>
    public void ExecuteBatch(params ReadOnlySpan<GpuOperation> operations)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (ref readonly var op in operations)
        {
            using var buffer = new GpuBufferScope(this, op.RequiredSize);
            op.Execute(buffer.Buffer);
        }
    }

    private GpuBuffer AllocateNewBuffer(nuint size)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _allocationLock.Wait();

            if (_totalAllocated >= _maxBufferCount)
            {
                throw new InvalidOperationException($"GPU memory pool exhausted. Max buffers: {_maxBufferCount}");
            }

            var buffer = GpuBuffer.Allocate(size);
            _allocatedBuffers.TryAdd(buffer.DevicePointer, buffer);

            Interlocked.Increment(ref _totalAllocated);
            Interlocked.Add(ref _totalBytesAllocated, (long)size);
            Interlocked.Add(ref s_globalTotalBytes, (long)size);

            s_allocationsCounter.Add(1);
            s_activeBuffers.Add(1);
            s_allocationDuration.Record((DateTime.UtcNow - startTime).TotalMilliseconds);

            return buffer;
        }
        finally
        {
            _allocationLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Free all allocated buffers
        while (_availableBuffers.TryDequeue(out var buffer))
        {
            buffer.Dispose();
        }

        foreach (var kvp in _allocatedBuffers)
        {
            kvp.Value.Dispose();
        }

        _allocatedBuffers.Clear();
        _allocationLock.Dispose();

        Interlocked.Add(ref s_globalTotalBytes, -Interlocked.Read(ref _totalBytesAllocated));
    }
}

/// <summary>
/// Represents a GPU device buffer with zero-copy host access.
/// </summary>
public sealed class GpuBuffer : IDisposable
{
    private nint _devicePointer;
    private nint _hostPointer;
    private readonly nuint _size;
    private bool _disposed;

    private GpuBuffer(nint devicePointer, nint hostPointer, nuint size)
    {
        _devicePointer = devicePointer;
        _hostPointer = hostPointer;
        _size = size;
    }

    public nint DevicePointer => _devicePointer;
    public nint HostPointer => _hostPointer;
    public nuint Size => _size;

    /// <summary>
    /// Gets a span over the host-mapped memory for zero-copy access.
    /// </summary>
    public unsafe Span<byte> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new Span<byte>((void*)_hostPointer, (int)_size);
    }

    /// <summary>
    /// Gets a memory over the host-mapped memory for async operations.
    /// Note: For GPU buffers, this creates a managed copy. For CPU buffers, it wraps the pinned array.
    /// </summary>
    public Memory<byte> AsMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Create a managed array copy for Memory<T> compatibility with async methods
        // This is less efficient than Span but necessary for async I/O
        var array = new byte[(int)_size];
        AsSpan().CopyTo(array);
        return array;
    }

    /// <summary>
    /// Gets a span of the specified type over host-mapped memory.
    /// </summary>
    public unsafe Span<T> AsSpan<T>() where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new Span<T>((void*)_hostPointer, (int)(_size / (nuint)sizeof(T)));
    }

    /// <summary>
    /// Allocates a new GPU buffer with host-mapped memory.
    /// </summary>
    public static GpuBuffer Allocate(nuint size)
    {
        // Try CUDA allocation first, fall back to managed memory
        if (NativeTensorContext.InitTensorContext() > 0)
        {
            var devicePtr = NativeTensorContext.AllocateDeviceMemory(size);
            var hostPtr = NativeTensorContext.MapHostMemory(devicePtr, size);
            return new GpuBuffer(devicePtr, hostPtr, size);
        }

        // CPU fallback with pinned memory
        var pinnedArray = GC.AllocateArray<byte>((int)size, pinned: true);
        unsafe
        {
            fixed (byte* ptr = pinnedArray)
            {
                return new GpuBuffer(nint.Zero, (nint)ptr, size);
            }
        }
    }

    /// <summary>
    /// Resets the buffer for reuse without deallocation.
    /// </summary>
    public void Reset()
    {
        if (_hostPointer != nint.Zero)
        {
            AsSpan().Clear();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_devicePointer != nint.Zero)
        {
            NativeTensorContext.FreeDeviceMemory(_devicePointer);
        }
    }
}

/// <summary>
/// RAII scope for GPU buffer usage.
/// </summary>
public readonly ref struct GpuBufferScope
{
    private readonly GpuMemoryPool _pool;
    public readonly GpuBuffer Buffer;

    public GpuBufferScope(GpuMemoryPool pool, nuint size)
    {
        _pool = pool;
        Buffer = pool.Rent(size);
    }

    public void Dispose()
    {
        _pool.Return(Buffer);
    }
}

/// <summary>
/// Represents a GPU compute operation for batched execution.
/// </summary>
public readonly record struct GpuOperation(nuint RequiredSize, Action<GpuBuffer> Execute);
