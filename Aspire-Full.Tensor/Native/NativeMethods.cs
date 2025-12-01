using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Aspire_Full.Tensor.Native;

[StructLayout(LayoutKind.Sequential)]
public struct TensorMetrics
{
    public float ComputeTimeMs;
    public float MemoryUsageMb;
    public int ActiveKernels;
}

public static partial class NativeMethods
{
    private const string DllName = "AspireFullNative";

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int InitTensorContext();

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ComputeTensorOp(
        [In] float[] a,
        [In] float[] b,
        [Out] float[] result,
        int numElements,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ValidateTensorContent(
        [In] float[] data,
        int numElements,
        float threshold,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void MatrixMultiply(
        [In] float[] a,
        [In] float[] b,
        [Out] float[] result,
        int M,
        int N,
        int K,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void MeanPooling(
        [In] float[] input,
        [In] long[] attentionMask,
        [Out] float[] output,
        int batchSize,
        int seqLen,
        int hiddenSize,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ReluActivation(
        [In] float[] input,
        [Out] float[] output,
        int numElements,
        ref TensorMetrics metrics);
}
