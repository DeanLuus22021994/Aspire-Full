#include <cuda_runtime.h>
#include <stdio.h>

#ifdef _WIN32
#define EXPORT extern "C" __declspec(dllexport)
#else
#define EXPORT extern "C"
#endif

struct TensorMetrics {
    float compute_time_ms;
    float memory_usage_mb;
    int active_kernels;
};

// CUDA Kernel for vector addition (simulating a tensor op)
__global__ void vectorAdd(const float *A, const float *B, float *C, int numElements) {
    int i = blockDim.x * blockIdx.x + threadIdx.x;
    if (i < numElements) {
        // Simulate heavy compute
        float val = A[i] + B[i];
        for(int k=0; k<10; k++) {
            val = val * 1.001f;
        }
        C[i] = val;
    }
}

EXPORT int InitTensorContext() {
    int deviceCount = 0;
    cudaError_t err = cudaGetDeviceCount(&deviceCount);
    if (err != cudaSuccess) return -1;
    return deviceCount;
}

EXPORT void ComputeTensorOp(const float* h_A, const float* h_B, float* h_C, int numElements, TensorMetrics* metrics) {
    // Allocate device memory
    float *d_A = NULL, *d_B = NULL, *d_C = NULL;
    size_t size = numElements * sizeof(float);

    cudaEvent_t start, stop;
    cudaEventCreate(&start);
    cudaEventCreate(&stop);

    if (cudaMalloc((void **)&d_A, size) != cudaSuccess) return;
    if (cudaMalloc((void **)&d_B, size) != cudaSuccess) { cudaFree(d_A); return; }
    if (cudaMalloc((void **)&d_C, size) != cudaSuccess) { cudaFree(d_A); cudaFree(d_B); return; }

    // Copy host to device
    cudaMemcpy(d_A, h_A, size, cudaMemcpyHostToDevice);
    cudaMemcpy(d_B, h_B, size, cudaMemcpyHostToDevice);

    // Launch Kernel
    int threadsPerBlock = 256;
    int blocksPerGrid = (numElements + threadsPerBlock - 1) / threadsPerBlock;

    cudaEventRecord(start);
    vectorAdd<<<blocksPerGrid, threadsPerBlock>>>(d_A, d_B, d_C, numElements);
    cudaEventRecord(stop);

    // Copy result back
    cudaMemcpy(h_C, d_C, size, cudaMemcpyDeviceToHost);

    cudaEventSynchronize(stop);
    float milliseconds = 0;
    cudaEventElapsedTime(&milliseconds, start, stop);

    // Update metrics
    if (metrics != NULL) {
        metrics->compute_time_ms = milliseconds;
        size_t free_byte, total_byte;
        cudaMemGetInfo(&free_byte, &total_byte);
        metrics->memory_usage_mb = (float)(total_byte - free_byte) / 1024.0f / 1024.0f;
        metrics->active_kernels = 1;
    }

    // Cleanup
    cudaFree(d_A);
    cudaFree(d_B);
    cudaFree(d_C);
    cudaEventDestroy(start);
    cudaEventDestroy(stop);
}

EXPORT int ValidateTensorContent(const float* h_Data, int numElements, float threshold, TensorMetrics* metrics) {
    // Allocate device memory
    float *d_Data = NULL;
    size_t size = numElements * sizeof(float);

    if (cudaMalloc((void **)&d_Data, size) != cudaSuccess) return -1;
    cudaMemcpy(d_Data, h_Data, size, cudaMemcpyHostToDevice);

    // Placeholder for actual validation kernel
    // For now, we just simulate a check

    cudaFree(d_Data);

    if (metrics != NULL) {
        metrics->active_kernels++;
    }

    return 1; // Valid
}
