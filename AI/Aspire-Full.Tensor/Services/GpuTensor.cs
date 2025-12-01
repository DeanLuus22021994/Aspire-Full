using System;
using Aspire_Full.Tensor.Core.Native;

namespace Aspire_Full.Tensor.Services;

public class GpuTensor<T> : IDisposable where T : unmanaged
{
    public IntPtr Pointer { get; private set; }
    public int Length { get; }
    public nuint SizeBytes { get; }
    private bool _disposed;

    public GpuTensor(int length)
    {
        Length = length;
        SizeBytes = (nuint)(length * System.Runtime.InteropServices.Marshal.SizeOf<T>());

        if (typeof(T) == typeof(long))
        {
            Pointer = NativeTensorContext.AllocateDeviceMemoryLong(SizeBytes);
        }
        else
        {
            Pointer = NativeTensorContext.AllocateDeviceMemory(SizeBytes);
        }

        if (Pointer == IntPtr.Zero)
        {
            throw new OutOfMemoryException("Failed to allocate GPU memory.");
        }
    }

    public void Upload(T[] data)
    {
        if (data.Length != Length)
            throw new ArgumentException("Data length mismatch");

        if (typeof(T) == typeof(float))
        {
            NativeTensorContext.CopyToDevice(Pointer, (float[])(object)data, SizeBytes);
        }
        else if (typeof(T) == typeof(long))
        {
            NativeTensorContext.CopyToDeviceLong(Pointer, (long[])(object)data, SizeBytes);
        }
        else
        {
            throw new NotSupportedException($"Type {typeof(T)} not supported for upload");
        }
    }

    public void Download(T[] destination)
    {
        if (destination.Length != Length)
            throw new ArgumentException("Destination length mismatch");

        if (typeof(T) == typeof(float))
        {
            NativeTensorContext.CopyToHost((float[])(object)destination, Pointer, SizeBytes);
        }
        else
        {
            throw new NotSupportedException($"Type {typeof(T)} not supported for download");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (Pointer != IntPtr.Zero)
            {
                if (typeof(T) == typeof(long))
                {
                    NativeTensorContext.FreeDeviceMemoryLong(Pointer);
                }
                else
                {
                    NativeTensorContext.FreeDeviceMemory(Pointer);
                }
                Pointer = IntPtr.Zero;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~GpuTensor()
    {
        Dispose();
    }
}
