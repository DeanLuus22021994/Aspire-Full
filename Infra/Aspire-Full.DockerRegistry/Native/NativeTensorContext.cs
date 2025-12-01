using System;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Aspire_Full.DockerRegistry.Native;

/// <summary>
/// Native CUDA interop context for GPU-accelerated tensor operations.
/// Uses LibraryImport (C# 14) with ReadOnlySpan overloads for zero-copy performance.
/// Provides portable fallback via TensorPrimitives when GPU unavailable.
/// </summary>
public static partial class NativeTensorContext
{
    private const string DllName = "AspireFullNative";
    private static int s_gpuDeviceCount = -1;
    private static readonly object s_initLock = new();

    #region Structs

    [StructLayout(LayoutKind.Sequential)]
    public struct TensorMetrics
    {
        public float compute_time_ms;
        public float memory_usage_mb;
        public int active_kernels;
        public int gpu_utilization_percent;
        public long total_flops;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GpuDeviceInfo
    {
        public int device_id;
        public long total_memory;
        public long free_memory;
        public int compute_capability_major;
        public int compute_capability_minor;
        public int multiprocessor_count;
        public int max_threads_per_block;
    }

    #endregion

    #region Core Initialization

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int InitTensorContext();

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int GetDeviceCount();

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int GetDeviceInfo(int deviceId, out GpuDeviceInfo info);

    /// <summary>
    /// Thread-safe GPU device count with lazy initialization.
    /// </summary>
    public static int GpuDeviceCount
    {
        get
        {
            if (s_gpuDeviceCount < 0)
            {
                lock (s_initLock)
                {
                    if (s_gpuDeviceCount < 0)
                    {
                        try
                        {
                            if (InitTensorContext() > 0)
                            {
                                s_gpuDeviceCount = GetDeviceCount();
                            }
                            else
                            {
                                s_gpuDeviceCount = 0;
                            }
                        }
                        catch
                        {
                            s_gpuDeviceCount = 0;
                        }
                    }
                }
            }
            return s_gpuDeviceCount;
        }
    }

    /// <summary>
    /// Returns true if GPU compute is available.
    /// </summary>
    public static bool IsGpuAvailable => GpuDeviceCount > 0;

    #endregion

    #region Memory Management

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint AllocateDeviceMemory(nuint size);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void FreeDeviceMemory(nint devicePtr);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint MapHostMemory(nint devicePtr, nuint size);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void UnmapHostMemory(nint hostPtr);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int SynchronizeDevice();

    #endregion

    #region Tensor Operations - GPU Path

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ComputeTensorOp(
        [In] float[] A,
        [In] float[] B,
        [Out] float[] C,
        int numElements,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ValidateTensorContent(
        [In] float[] Data,
        int numElements,
        float threshold,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int HashTensorContent(
        nint dataPtr,
        nuint size,
        [Out] byte[] hashOut,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int CompressLayerData(
        nint inputPtr,
        nuint inputSize,
        nint outputPtr,
        ref nuint outputSize,
        int compressionLevel,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ValidateManifestHash(
        nint manifestPtr,
        nuint manifestSize,
        [In] byte[] expectedHash,
        ref TensorMetrics metrics);

    #endregion

    #region Portable Operations - TensorPrimitives Fallback

    /// <summary>
    /// Computes cosine similarity between two vectors.
    /// Uses GPU when available, falls back to TensorPrimitives SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CosineSimilarity(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        if (IsGpuAvailable)
        {
            // GPU path - pin and dispatch to CUDA kernel
            return CosineSimilarityGpu(x, y);
        }

        // CPU SIMD fallback using TensorPrimitives
        return TensorPrimitives.CosineSimilarity(x, y);
    }

    /// <summary>
    /// Computes the L2 norm of a vector.
    /// Uses GPU when available, falls back to TensorPrimitives SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Norm(ReadOnlySpan<float> x)
    {
        if (IsGpuAvailable)
        {
            return NormGpu(x);
        }

        return TensorPrimitives.Norm(x);
    }

    /// <summary>
    /// Computes dot product of two vectors.
    /// Uses GPU when available, falls back to TensorPrimitives SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        if (IsGpuAvailable)
        {
            return DotGpu(x, y);
        }

        return TensorPrimitives.Dot(x, y);
    }

    /// <summary>
    /// Applies softmax activation in-place.
    /// Uses GPU when available, falls back to TensorPrimitives SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SoftMax(ReadOnlySpan<float> x, Span<float> destination)
    {
        if (IsGpuAvailable)
        {
            SoftMaxGpu(x, destination);
            return;
        }

        TensorPrimitives.SoftMax(x, destination);
    }

    /// <summary>
    /// Validates tensor content with threshold check.
    /// Uses GPU when available, falls back to TensorPrimitives.
    /// </summary>
    public static bool ValidateContent(ReadOnlySpan<float> data, float threshold, out TensorMetrics metrics)
    {
        metrics = default;

        if (IsGpuAvailable)
        {
            var result = ValidateTensorContent(data.ToArray(), data.Length, threshold, ref metrics);
            return result == 1;
        }

        // CPU fallback: check if max absolute value is within threshold
        var maxAbs = TensorPrimitives.MaxMagnitude(data);
        metrics.compute_time_ms = 0;
        metrics.memory_usage_mb = data.Length * sizeof(float) / (1024f * 1024f);
        return maxAbs <= threshold;
    }

    #endregion

    #region Private GPU Dispatch Methods

    private static unsafe float CosineSimilarityGpu(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        fixed (float* xPtr = x)
        fixed (float* yPtr = y)
        {
            var metrics = new TensorMetrics();
            float result = 0;
            // Call native CUDA kernel for cosine similarity
            CosineSimilarityKernel((nint)xPtr, (nint)yPtr, x.Length, &result, ref metrics);
            return result;
        }
    }

    private static unsafe float NormGpu(ReadOnlySpan<float> x)
    {
        fixed (float* xPtr = x)
        {
            var metrics = new TensorMetrics();
            float result = 0;
            NormKernel((nint)xPtr, x.Length, &result, ref metrics);
            return result;
        }
    }

    private static unsafe float DotGpu(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        fixed (float* xPtr = x)
        fixed (float* yPtr = y)
        {
            var metrics = new TensorMetrics();
            float result = 0;
            DotKernel((nint)xPtr, (nint)yPtr, x.Length, &result, ref metrics);
            return result;
        }
    }

    private static unsafe void SoftMaxGpu(ReadOnlySpan<float> x, Span<float> destination)
    {
        fixed (float* xPtr = x)
        fixed (float* dPtr = destination)
        {
            var metrics = new TensorMetrics();
            SoftMaxKernel((nint)xPtr, (nint)dPtr, x.Length, ref metrics);
        }
    }

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial void CosineSimilarityKernel(nint x, nint y, int length, float* result, ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial void NormKernel(nint x, int length, float* result, ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial void DotKernel(nint x, nint y, int length, float* result, ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void SoftMaxKernel(nint x, nint destination, int length, ref TensorMetrics metrics);

    #endregion
}
