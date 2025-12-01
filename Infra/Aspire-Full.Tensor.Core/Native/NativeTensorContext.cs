using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Aspire_Full.Tensor.Core.Native;

/// <summary>
/// Unified native CUDA interop context for GPU-accelerated tensor operations.
/// Provides portable fallback via TensorPrimitives when GPU unavailable.
/// Single source of truth for all P/Invoke definitions to AspireFullNative library.
/// </summary>
public static partial class NativeTensorContext
{
    private const string DllName = "AspireFullNative";
    private static int s_gpuDeviceCount = -1;
    private static readonly object s_initLock = new();

    /// <summary>
    /// Static constructor ensures NativeLibraryResolver is initialized before any P/Invoke calls.
    /// </summary>
    static NativeTensorContext()
    {
        NativeLibraryResolver.Initialize();
    }

    #region Structs

    /// <summary>
    /// Unified tensor metrics struct matching native layout.
    /// Contains all fields from both DockerRegistry and ML compute contexts.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TensorMetrics
    {
        public float compute_time_ms;
        public float memory_usage_mb;
        public int active_kernels;
        public int gpu_utilization_percent;
        public long total_flops;
        // Extended metrics for detailed profiling
        public float hash_time_ms;
        public float compress_time_ms;
        public float transfer_time_ms;
    }

    /// <summary>
    /// GPU device information for capability detection.
    /// </summary>
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
        public int warp_size;
        public int max_shared_memory_per_block;
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

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int SetDevice(int deviceId);

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

    /// <summary>
    /// Resets the cached GPU device count to force re-detection.
    /// </summary>
    public static void ResetDeviceCache()
    {
        lock (s_initLock)
        {
            s_gpuDeviceCount = -1;
        }
    }

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

    // Long/Int64 memory variants for embedding tensors
    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint AllocateDeviceMemoryLong(nuint size);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void FreeDeviceMemoryLong(nint devicePtr);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void CopyToDevice(nint deviceDst, [In] float[] hostSrc, nuint sizeBytes);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void CopyToHost([Out] float[] hostDst, nint deviceSrc, nuint sizeBytes);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void CopyToDeviceLong(nint deviceDst, [In] long[] hostSrc, nuint sizeBytes);

    #endregion

    #region Core Tensor Operations

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

    #region ML Compute Operations (from AI/Tensor)

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void MatrixMultiply_GPU(
        nint deviceA,
        nint deviceB,
        nint deviceC,
        int M,
        int N,
        int K,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void MeanPooling_GPU(
        nint deviceInput,
        nint deviceAttentionMask,
        nint deviceOutput,
        int batchSize,
        int seqLen,
        int hiddenSize,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ReluActivation_GPU(
        nint deviceInput,
        nint deviceOutput,
        int numElements,
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
            return CosineSimilarityGpu(x, y);
        }
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
    /// Applies softmax activation.
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
    /// Applies ReLU activation.
    /// Uses GPU when available, falls back to element-wise max.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReLU(ReadOnlySpan<float> x, Span<float> destination)
    {
        if (IsGpuAvailable)
        {
            ReLUGpu(x, destination);
            return;
        }
        // CPU fallback
        for (int i = 0; i < x.Length; i++)
        {
            destination[i] = Math.Max(0, x[i]);
        }
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

    /// <summary>
    /// Performs element-wise addition: destination = x + y.
    /// Uses GPU when available, falls back to TensorPrimitives SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> destination)
    {
        if (IsGpuAvailable && x.Length >= 4096)
        {
            AddGpu(x, y, destination);
            return;
        }
        TensorPrimitives.Add(x, y, destination);
    }

    /// <summary>
    /// Performs element-wise multiplication: destination = x * y.
    /// Uses GPU when available, falls back to TensorPrimitives SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> destination)
    {
        if (IsGpuAvailable && x.Length >= 4096)
        {
            MultiplyGpu(x, y, destination);
            return;
        }
        TensorPrimitives.Multiply(x, y, destination);
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

    private static unsafe void ReLUGpu(ReadOnlySpan<float> x, Span<float> destination)
    {
        fixed (float* xPtr = x)
        fixed (float* dPtr = destination)
        {
            var metrics = new TensorMetrics();
            ReluActivation_GPU((nint)xPtr, (nint)dPtr, x.Length, ref metrics);
        }
    }

    private static unsafe void AddGpu(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> destination)
    {
        fixed (float* xPtr = x)
        fixed (float* yPtr = y)
        fixed (float* dPtr = destination)
        {
            var metrics = new TensorMetrics();
            // Use ComputeTensorOp for element-wise add
            ComputeTensorOp(x.ToArray(), y.ToArray(), destination.ToArray(), x.Length, ref metrics);
        }
    }

    private static unsafe void MultiplyGpu(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> destination)
    {
        fixed (float* xPtr = x)
        fixed (float* yPtr = y)
        fixed (float* dPtr = destination)
        {
            var metrics = new TensorMetrics();
            MultiplyKernel((nint)xPtr, (nint)yPtr, (nint)dPtr, x.Length, ref metrics);
        }
    }

    #endregion

    #region Native Kernel Imports

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

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void MultiplyKernel(nint x, nint y, nint destination, int length, ref TensorMetrics metrics);

    #endregion
}
