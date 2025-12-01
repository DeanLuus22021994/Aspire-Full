using System;
using Aspire_Full.Tensor.Native;

namespace Aspire_Full.Tensor.Services;

public class GpuTensor<T> : IDisposable where T : unmanaged
{
    public IntPtr Pointer { get; private set; }
    public int Length { get; }
    public ulong SizeBytes { get; }
    private bool _disposed;

    public GpuTensor(int length)
    {
        Length = length;
        SizeBytes = (ulong)(length * System.Runtime.InteropServices.Marshal.SizeOf<T>());

        if (typeof(T) == typeof(long))
        {
            Pointer = NativeMethods.AllocateDeviceMemoryLong(SizeBytes);
        }
        else
        {
            Pointer = NativeMethods.AllocateDeviceMemory(SizeBytes);
        }

        if (Pointer == IntPtr.Zero)
        {
            throw new OutOfMemoryException("Failed to allocate GPU memory.");
        }
    }

    public void Upload(T[] data)
    {
        if (data.Length != Length) throw new ArgumentException("Data length mismatch");

        if (typeof(T) == typeof(float))
        {
            NativeMethods.CopyToDevice(Pointer, (float[])(object)data, SizeBytes);
        }
        else if (typeof(T) == typeof(long))
        {
            NativeMethods.CopyToDeviceLong(Pointer, (long[])(object)data, SizeBytes);
        }
        else
        {
            throw new NotSupportedException($"Type {typeof(T)} not supported for upload");
        }
    }

    public void Download(T[] destination)
    {
        if (destination.Length != Length) throw new ArgumentException("Destination length mismatch");

        if (typeof(T) == typeof(float))
        {
            NativeMethods.CopyToHost((float[])(object)destination, Pointer, SizeBytes);
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
                    NativeMethods.FreeDeviceMemoryLong(Pointer);
                }
                else
                {
                    NativeMethods.FreeDeviceMemory(Pointer);
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
