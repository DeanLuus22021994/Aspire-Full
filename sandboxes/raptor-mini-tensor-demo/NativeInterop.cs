using System.Runtime.InteropServices;

internal static class NativeInterop
{
    private const string DllName = "AspireFullNative";

    [StructLayout(LayoutKind.Sequential)]
    public struct TensorMetrics
    {
        public float compute_time_ms;
        public float memory_usage_mb;
        public int active_kernels;
        public int gpu_utilization_percent;
        public long total_flops;
        public float hash_time_ms;
        public float compress_time_ms;
        public float transfer_time_ms;
    }

    [DllImport(DllName, EntryPoint = "InitTensorContext", CallingConvention = CallingConvention.Cdecl)]
    public static extern int InitTensorContext();

    [DllImport(DllName, EntryPoint = "GetDeviceCount", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetDeviceCount();

    [DllImport(DllName, EntryPoint = "AllocateDeviceMemory", CallingConvention = CallingConvention.Cdecl)]
    public static extern nint AllocateDeviceMemory(nuint size);

    [DllImport(DllName, EntryPoint = "FreeDeviceMemory", CallingConvention = CallingConvention.Cdecl)]
    public static extern void FreeDeviceMemory(nint devicePtr);

    [DllImport(DllName, EntryPoint = "CopyToDevice", CallingConvention = CallingConvention.Cdecl)]
    public static extern void CopyToDevice(nint deviceDst, float[] hostSrc, nuint sizeBytes);

    [DllImport(DllName, EntryPoint = "CopyToHost", CallingConvention = CallingConvention.Cdecl)]
    public static extern void CopyToHost(float[] hostDst, nint deviceSrc, nuint sizeBytes);

    [DllImport(DllName, EntryPoint = "MatrixMultiply_GPU", CallingConvention = CallingConvention.Cdecl)]
    public static extern void MatrixMultiply_GPU(nint deviceA, nint deviceB, nint deviceC, int M, int N, int K, ref TensorMetrics metrics);
}
