/**
 * @file tensor_ops.cu
 * @brief CUDA implementation of tensor operations for the Aspire-Full Native library.
 *
 * This file contains CUDA kernels and host wrapper functions to perform
 * tensor computations, including initialization, vector addition, and validation.
 * It exports C-compatible functions for interoperability with other languages.
 */

#include <cuda_runtime.h>
#include <stdio.h>

#ifdef __INTELLISENSE__
#ifndef __CUDACC__
#define __global__
#define __device__
#define __host__
typedef int cudaError_t;
typedef int cudaEvent_t;
#define cudaSuccess 0
#define cudaMemcpyHostToDevice 1
#define cudaMemcpyDeviceToHost 2
struct dim3 { int x, y, z; };
extern dim3 blockDim;
extern dim3 blockIdx;
extern dim3 threadIdx;
extern "C" int cudaGetDeviceCount(int*);
extern "C" int cudaMalloc(void**, size_t);
extern "C" int cudaFree(void*);
extern "C" int cudaMemcpy(void*, const void*, size_t, int);
extern "C" int cudaEventCreate(cudaEvent_t*);
extern "C" int cudaEventRecord(cudaEvent_t, int stream = 0);
extern "C" int cudaEventSynchronize(cudaEvent_t);
extern "C" int cudaEventElapsedTime(float*, cudaEvent_t, cudaEvent_t);
extern "C" int cudaEventDestroy(cudaEvent_t);
extern "C" int cudaMemGetInfo(size_t*, size_t*);
#endif
#endif

#if defined(_WIN32) && !defined(__clang__)
#define EXPORT extern "C" __declspec(dllexport)
#elif defined(_WIN32) && defined(__clang__)
#define EXPORT extern "C" __attribute__((dllexport))
#else
#define EXPORT extern "C" __attribute__((visibility("default")))
#endif

struct TensorMetrics {
    float compute_time_ms;
    float memory_usage_mb;
    int active_kernels;
};

/**
 * @brief CUDA Kernel for vector addition.
 *
 * Performs element-wise addition of two vectors A and B into C.
 * Simulates heavy computation with a loop.
 *
 * @param A Input vector A
 * @param B Input vector B
 * @param C Output vector C
 * @param numElements Number of elements in the vectors
 */
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

/**
 * @brief Initializes the tensor context.
 *
 * Checks for available CUDA devices.
 *
 * @return Number of CUDA devices found, or -1 on error.
 */
EXPORT int InitTensorContext() {
    int deviceCount = 0;
    cudaError_t err = cudaGetDeviceCount(&deviceCount);
    if (err != cudaSuccess) return -1;
    return deviceCount;
}

/**
 * @brief Computes a tensor operation (Vector Addition).
 *
 * Allocates device memory, copies data, executes the kernel, and retrieves results.
 * Also captures performance metrics.
 *
 * @param h_A Host input array A
 * @param h_B Host input array B
 * @param h_C Host output array C
 * @param numElements Number of elements
 * @param metrics Pointer to TensorMetrics structure to populate
 */
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

/**
 * @brief Validates tensor content.
 *
 * Simulates a validation step on the GPU.
 *
 * @param h_Data Host data array to validate
 * @param numElements Number of elements
 * @param threshold Validation threshold
 * @param metrics Pointer to TensorMetrics structure to update
 * @return 1 if valid, -1 on error
 */
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
