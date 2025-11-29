using System;
using System.Runtime.InteropServices;

namespace Aspire_Full.DockerRegistry.Native;

public static class NativeTensorContext
{
    private const string DllName = "AspireFullNative";

    [StructLayout(LayoutKind.Sequential)]
    public struct TensorMetrics
    {
        public float compute_time_ms;
        public float memory_usage_mb;
        public int active_kernels;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int InitTensorContext();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ComputeTensorOp(
        [In] float[] A,
        [In] float[] B,
        [Out] float[] C,
        int numElements,
        ref TensorMetrics metrics);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ValidateTensorContent(
        [In] float[] Data,
        int numElements,
        float threshold,
        ref TensorMetrics metrics);
}
