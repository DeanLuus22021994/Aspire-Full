using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Aspire_Full.DockerRegistry.Native;

public static partial class NativeTensorContext
{
    private const string DllName = "AspireFullNative";

    [StructLayout(LayoutKind.Sequential)]
    public struct TensorMetrics
    {
        public float compute_time_ms;
        public float memory_usage_mb;
        public int active_kernels;
    }

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int InitTensorContext();

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
}
