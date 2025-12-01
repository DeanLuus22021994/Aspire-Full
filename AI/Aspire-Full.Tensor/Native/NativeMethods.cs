using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
    public static partial IntPtr AllocateDeviceMemory(ulong sizeBytes);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void FreeDeviceMemory(IntPtr d_ptr);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void CopyToDevice(IntPtr d_dst, [In] float[] h_src, ulong sizeBytes);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void CopyToHost([Out] float[] h_dst, IntPtr d_src, ulong sizeBytes);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial IntPtr AllocateDeviceMemoryLong(ulong sizeBytes);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void FreeDeviceMemoryLong(IntPtr d_ptr);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void CopyToDeviceLong(IntPtr d_dst, [In] long[] h_src, ulong sizeBytes);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void MatrixMultiply_GPU(
        IntPtr d_A,
        IntPtr d_B,
        IntPtr d_C,
        int M,
        int N,
        int K,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void MeanPooling_GPU(
        IntPtr d_Input,
        IntPtr d_AttentionMask,
        IntPtr d_Output,
        int batchSize,
        int seqLen,
        int hiddenSize,
        ref TensorMetrics metrics);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void ReluActivation_GPU(
        IntPtr d_Input,
        IntPtr d_Output,
        int numElements,
        ref TensorMetrics metrics);
}
