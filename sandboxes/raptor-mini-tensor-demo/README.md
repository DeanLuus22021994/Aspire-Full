Raptor Mini Tensor Demo

Small console app demonstrating high-performance matrix multiplication on CPU using vectorization and parallelization.

Build

```bash
cd sandboxes/raptor-mini-tensor-demo
dotnet build -c Release
```

Run

```bash
# N size is optional; default is 1024
dotnet run -c Release -- 512
```

GPU (Native) support

- Requires the AspireFullNative library (libAspireFullNative.so) to be available on the PATH or in the runtime directory.
- Build the native library using the repository's native build script or task: `native:build` task in `Tasks` (invokes CMake and builds `libAspireFullNative.so`).
- Run with `--mode=native` to attempt a native GPU matmul using the native library.

Examples

- CPU only: `dotnet run -c Release -- --size=512 --mode=cpu --iterations=1`
- Native GPU (if compiled): `dotnet run -c Release -- --size=512 --mode=native --iterations=1`
- All modes: `dotnet run -c Release -- --size=512 --mode=all --iterations=1`

Outputs

- `raptor-bench.csv` is written to the sandbox directory and contains timestamped results with GFLOPS and validation against baseline (Parallel algorithm).


Notes

- Uses System.Numerics.Vector<float> for SIMD vectorization.
- Uses Parallel.For for multi-core computation.
- For GPU acceleration tests, consider adding an ONNX model and using Microsoft.ML.OnnxRuntime.Gpu provider.
