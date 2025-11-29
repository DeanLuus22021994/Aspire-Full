using System.Runtime.InteropServices;

namespace Aspire_Full.Tensor.Native;

[StructLayout(LayoutKind.Sequential)]
public struct TensorMetrics
{
    public float ComputeTimeMs;
    public float MemoryUsageMb;
    public int ActiveKernels;
}

public static class NativeMethods
{
    private const string DllName = "AspireFullNative";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int InitTensorContext();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ComputeTensorOp(
        [In] float[] a,
        [In] float[] b,
        [Out] float[] result,
        int numElements,
        ref TensorMetrics metrics);
}
