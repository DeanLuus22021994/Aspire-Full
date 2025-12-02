using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;

using System.Runtime.InteropServices;

// NativeInterop type is declared in NativeInterop.cs (keeps types out of top-level statements)

int argIndex = 0;
int N = 1024; // matrix N x N
string mode = "cpu"; // cpu | native
int iterations = 1;
for (; argIndex < args.Length; argIndex++)
{
    var arg = args[argIndex];
    if (arg.StartsWith("--"))
    {
        var parts = arg.Substring(2).Split('=');
        switch (parts[0])
        {
            case "size": if (parts.Length > 1 && int.TryParse(parts[1], out var s)) N = s; break;
            case "mode": if (parts.Length > 1) mode = parts[1]; break;
            case "iterations": if (parts.Length > 1 && int.TryParse(parts[1], out var it)) iterations = it; break;
        }
    }
    else if (int.TryParse(arg, out var n))
    {
        N = n;
    }
}

Console.WriteLine($"Raptor Mini Tensor Demo - Matrix Multiply n={N} mode={mode} iterations={iterations}");

float[] MakeMatrix(int n)
{
    var r = new Random(42);
    var a = new float[n * n];
    for (int i = 0; i < a.Length; i++) a[i] = (float)r.NextDouble();
    return a;
}

var A = MakeMatrix(N);
var B = MakeMatrix(N);
var C = new float[N * N];
var baseline = new float[N * N];

long flops = (long)N * N * (2L * N - 1); // approximate flops for matmul

static void NaiveMatMul(float[] A, float[] B, float[] C, int n)
{
    Array.Clear(C, 0, C.Length);
    for (int i = 0; i < n; i++)
    {
        for (int j = 0; j < n; j++)
        {
            float sum = 0f;
            int ai = i * n;
            int bj = j;
            for (int k = 0; k < n; k++)
            {
                sum += A[ai + k] * B[k * n + j];
            }
            C[ai + j] = sum;
        }
    }
}

static void VectorizedMatMul(float[] A, float[] B, float[] C, int n)
{
    Array.Clear(C, 0, C.Length);
    int vectorWidth = Vector<float>.Count;
    for (int i = 0; i < n; i++)
    {
        for (int k = 0; k < n; k++)
        {
            int ai = i * n + k;
            float aVal = A[ai];
            int bIdx = k * n;

            int j = 0;
            var aVec = new Vector<float>(aVal);
            for (; j <= n - vectorWidth; j += vectorWidth)
            {
                var bVec = new Vector<float>(B, bIdx + j);
                var cVec = new Vector<float>(C, i * n + j);
                cVec += aVec * bVec;
                cVec.CopyTo(C, i * n + j);
            }
            for (; j < n; j++)
            {
                C[i * n + j] += aVal * B[bIdx + j];
            }
        }
    }
}

static void ParallelMatMul(float[] A, float[] B, float[] C, int n)
{
    Array.Clear(C, 0, C.Length);
    int vectorWidth = Vector<float>.Count;

    Parallel.For(0, n, i =>
    {
        for (int k = 0; k < n; k++)
        {
            int ai = i * n + k;
            float aVal = A[ai];
            int bIdx = k * n;
            int j = 0;
            var aVec = new Vector<float>(aVal);
            for (; j <= n - vectorWidth; j += vectorWidth)
            {
                var bVec = new Vector<float>(B, bIdx + j);
                var cVec = new Vector<float>(C, i * n + j);
                cVec += aVec * bVec;
                cVec.CopyTo(C, i * n + j);
            }
            for (; j < n; j++)
            {
                C[i * n + j] += aVal * B[bIdx + j];
            }
        }
    });
}

static (double seconds, double gflops) Benchmark(Action a, long flops, string name)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var sw = Stopwatch.StartNew();
    a();
    sw.Stop();
    double seconds = sw.Elapsed.TotalSeconds;
    double gflops = flops / 1e9 / seconds;
    Console.WriteLine($"{name}: {seconds:F4}s, {gflops:F2} GFLOPS");
    return (seconds, gflops);
}

static double ValidateResult(float[] expected, float[] actual, int n)
{
    double maxDiff = 0;
    for (int i = 0; i < n * n; i++)
    {
        var diff = Math.Abs(expected[i] - actual[i]);
        if (diff > maxDiff) maxDiff = diff;
    }
    if (maxDiff > 1e-3)
    {
        Console.WriteLine($"WARNING: Max mismatch against baseline: {maxDiff:E}");
    }
    else
    {
        Console.WriteLine($"Validation passed. Max diff: {maxDiff:E}");
    }
    return maxDiff;
}

static void LogResult(int n, string mode, string name, double seconds, double gflops, double maxDiff)
{
    var csv = Path.Combine(Environment.CurrentDirectory, "raptor-bench.csv");
    var header = "timestamp,n,mode,name,seconds,gflops,maxDiff";
    if (!File.Exists(csv)) File.WriteAllText(csv, header + Environment.NewLine);
    var line = $"{DateTime.UtcNow:o},{n},{mode},{name},{seconds:F6},{gflops:F6},{maxDiff:E}";
    File.AppendAllText(csv, line + Environment.NewLine);
}

// NativeInterop type is provided in NativeInterop.cs so top-level statements remain first

static void GpuNativeMatMul(float[] A, float[] B, float[] C, int n)
{
    // Ensure native library is present and GPU is available
    try
    {
        NativeInterop.InitTensorContext();
    }
    catch (DllNotFoundException)
    {
        throw new InvalidOperationException("native libAspireFullNative not found in PATH");
    }
    var devCount = NativeInterop.GetDeviceCount();
    if (devCount <= 0)
    {
        throw new InvalidOperationException("GPU native context not available");
    }

    ulong sizeBytes = (ulong)(n * n * sizeof(float));
    var metrics = new NativeInterop.TensorMetrics();
    var devA = NativeInterop.AllocateDeviceMemory((nuint)sizeBytes);
    var devB = NativeInterop.AllocateDeviceMemory((nuint)sizeBytes);
    var devC = NativeInterop.AllocateDeviceMemory((nuint)sizeBytes);
    try
    {
        NativeInterop.CopyToDevice(devA, A, (nuint)sizeBytes);
        NativeInterop.CopyToDevice(devB, B, (nuint)sizeBytes);
        NativeInterop.MatrixMultiply_GPU(devA, devB, devC, n, n, n, ref metrics);
        NativeInterop.CopyToHost(C, devC, (nuint)sizeBytes);
    }
    finally
    {
        NativeInterop.FreeDeviceMemory(devA);
        NativeInterop.FreeDeviceMemory(devB);
        NativeInterop.FreeDeviceMemory(devC);
    }
}

Console.WriteLine("Warmup single-thread Vectorized matmul (small)");
VectorizedMatMul(A, B, C, Math.Min(N, 128));

Console.WriteLine("Computing baseline (ParallelVectorized) result for correctness checks...");
ParallelMatMul(A, B, baseline, N);

Console.WriteLine("Running benchmarks...");
for (int it = 0; it < iterations; it++)
{
    Console.WriteLine($"Iteration {it + 1}/{iterations}");
        if (mode.Equals("cpu", StringComparison.OrdinalIgnoreCase) || mode.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
            var (s1, g1) = Benchmark(() => NaiveMatMul(A, B, C, N), flops, "Naive");
            var d1 = ValidateResult(baseline, C, N);
            LogResult(N, mode, "Naive", s1, g1, d1);

            var (s2, g2) = Benchmark(() => VectorizedMatMul(A, B, C, N), flops, "Vectorized");
            var d2 = ValidateResult(baseline, C, N);
            LogResult(N, mode, "Vectorized", s2, g2, d2);

            var (s3, g3) = Benchmark(() => ParallelMatMul(A, B, C, N), flops, "Parallel+Vectorized");
            var d3 = ValidateResult(baseline, C, N);
            LogResult(N, mode, "Parallel+Vectorized", s3, g3, d3);
    }
    if (mode.Equals("native", StringComparison.OrdinalIgnoreCase) || mode.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            // init
            NativeInterop.InitTensorContext();
            var devices = NativeInterop.GetDeviceCount();
            Console.WriteLine($"Native GPU devices: {devices}");
            if (devices > 0)
            {
                var (s4, g4) = Benchmark(() => GpuNativeMatMul(A, B, C, N), flops, "GPU-Native");
                var d4 = ValidateResult(baseline, C, N);
                LogResult(N, mode, "GPU-Native", s4, g4, d4);
            }
            else
            {
                Console.WriteLine("No GPU devices found for native path.");
            }
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("Native libAspireFullNative not found; skipping native GPU benchmark.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Native GPU test failed: {ex.Message}");
        }
    }
}

Console.WriteLine("Done.");
