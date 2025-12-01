# Aspire-Full.Tensor

This project provides high-performance tensor operations using CUDA acceleration. It consists of a managed .NET 10 layer and a native C++/CUDA layer.

## Architecture

- **Managed Layer (`Aspire-Full.Tensor`)**:
  - `GpuTensor<T>`: Handles GPU memory allocation and data transfer.
  - `TensorComputeService`: Exposes high-level operations (MatMul, Pooling, ReLU).
  - `NativeMethods`: P/Invoke definitions for the native library.

- **Native Layer (`Native/`)**:
  - `src/tensor_ops.cpp`: Implementation of CUDA kernels and memory management.
  - `CMakeLists.txt`: Build configuration for the native library.

## Build Process

The native library is built using Docker to ensure a consistent environment with the CUDA Toolkit.

1.  **Trigger**: The `.csproj` file contains a `BuildNative` target that runs before the build.
2.  **Command**: `docker buildx bake -f Aspire-Full.DockerRegistry/docker-bake.hcl native-lib`
3.  **Output**:
    - Linux: `build/libAspireFullNative.so`
    - Windows: `build/AspireFullNative.dll` (if configured for cross-compilation or local build)

## Development

To modify the CUDA kernels:
1.  Edit `Native/src/tensor_ops.cpp`.
2.  Run `dotnet build` to trigger the Docker build and update the shared library.

## Requirements

- Docker Desktop (with GPU support recommended for running tests)
- .NET 10 SDK
