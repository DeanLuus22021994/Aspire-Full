/**
 * @file tensor_ops.cpp
 * @brief CUDA implementation of tensor operations for the Aspire-Full Native library.
 *        Renamed to .cpp to trick VS Code IntelliSense into using C++ stubs while
 *        CMake handles the CUDA compilation.
 */

#include "../include/tensor_ops.h"

#if defined(__CUDACC__) && !defined(__INTELLISENSE__)
#include <stdio.h>
#include <cuda_runtime.h>
#else
#include "cuda_stubs.h"
inline float fmaxf(float x, float y) { return x > y ? x : y; }
#endif

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
 * @brief CUDA Kernel for Matrix Multiplication (C = A * B).
 * A: [M x K], B: [K x N], C: [M x N]
 */
__global__ void matMulKernel(const float* A, const float* B, float* C, int M, int N, int K) {
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;

    if (row < M && col < N) {
        float sum = 0.0f;
        for (int k = 0; k < K; ++k) {
            sum += A[row * K + k] * B[k * N + col];
        }
        C[row * N + col] = sum;
    }
}

/**
 * @brief CUDA Kernel for Mean Pooling.
 * Input: [Batch, Seq, Hidden]
 * Mask: [Batch, Seq]
 * Output: [Batch, Hidden]
 */
__global__ void meanPoolingKernel(const float* input, const long long* attentionMask, float* output, int batchSize, int seqLen, int hiddenSize) {
    int b = blockIdx.z; // Batch index
    int h = blockIdx.x * blockDim.x + threadIdx.x; // Hidden dimension index

    if (b < batchSize && h < hiddenSize) {
        float sum = 0.0f;
        int count = 0;

        for (int s = 0; s < seqLen; ++s) {
            // Check mask (assuming 1 is valid, 0 is padding)
            if (attentionMask[b * seqLen + s] == 1) {
                sum += input[(b * seqLen + s) * hiddenSize + h];
                count++;
            }
        }

        if (count > 0) {
            output[b * hiddenSize + h] = sum / count;
        } else {
            output[b * hiddenSize + h] = 0.0f;
        }
    }
}

/**
 * @brief CUDA Kernel for ReLU Activation.
 * y = max(0, x)
 */
__global__ void reluKernel(const float* input, float* output, int numElements) {
    int i = blockDim.x * blockIdx.x + threadIdx.x;
    if (i < numElements) {
        output[i] = fmaxf(0.0f, input[i]);
    }
}

/**
 * @brief Initializes the tensor context.
 *
 * Checks for available CUDA devices.
 *
 * @return Number of CUDA devices found, or -1 on error.
 */
// ...existing code...
EXPORT int InitTensorContext() {
    int deviceCount = 0;
    cudaError_t err = cudaGetDeviceCount(&deviceCount);
    if (err != cudaSuccess) return -1;
    return deviceCount;
}

// --- Memory Management ---

EXPORT float* AllocateDeviceMemory(size_t sizeBytes) {
    float* d_ptr = NULL;
    if (cudaMalloc((void**)&d_ptr, sizeBytes) != cudaSuccess) return NULL;
    return d_ptr;
}

EXPORT void FreeDeviceMemory(float* d_ptr) {
    cudaFree(d_ptr);
}

EXPORT void CopyToDevice(float* d_dst, const float* h_src, size_t sizeBytes) {
    cudaMemcpy(d_dst, h_src, sizeBytes, cudaMemcpyHostToDevice);
}

EXPORT void CopyToHost(float* h_dst, const float* d_src, size_t sizeBytes) {
    cudaMemcpy(h_dst, d_src, sizeBytes, cudaMemcpyDeviceToHost);
}

EXPORT long long* AllocateDeviceMemoryLong(size_t sizeBytes) {
    long long* d_ptr = NULL;
    if (cudaMalloc((void**)&d_ptr, sizeBytes) != cudaSuccess) return NULL;
    return d_ptr;
}

EXPORT void FreeDeviceMemoryLong(long long* d_ptr) {
    cudaFree(d_ptr);
}

EXPORT void CopyToDeviceLong(long long* d_dst, const long long* h_src, size_t sizeBytes) {
    cudaMemcpy(d_dst, h_src, sizeBytes, cudaMemcpyHostToDevice);
}

// --- Compute Operations (GPU) ---

EXPORT void MatrixMultiply_GPU(const float* d_A, const float* d_B, float* d_C, int M, int N, int K, TensorMetrics* metrics) {
    cudaEvent_t start, stop;
    cudaEventCreate(&start);
    cudaEventCreate(&stop);

    dim3 threadsPerBlock(16, 16);
    dim3 blocksPerGrid((N + threadsPerBlock.x - 1) / threadsPerBlock.x,
                       (M + threadsPerBlock.y - 1) / threadsPerBlock.y);

    cudaEventRecord(start);
#ifdef __CUDACC__
    matMulKernel<<<blocksPerGrid, threadsPerBlock>>>(d_A, d_B, d_C, M, N, K);
#else
    matMulKernel(d_A, d_B, d_C, M, N, K);
#endif
    cudaEventRecord(stop);
    cudaEventSynchronize(stop);

    float milliseconds = 0;
    cudaEventElapsedTime(&milliseconds, start, stop);

    if (metrics != NULL) {
        metrics->compute_time_ms = milliseconds;
        metrics->active_kernels = 1;
        // Memory usage is not tracked per-op here as we don't allocate
    }

    cudaEventDestroy(start);
    cudaEventDestroy(stop);
}

EXPORT void MeanPooling_GPU(const float* d_Input, const long long* d_AttentionMask, float* d_Output, int batchSize, int seqLen, int hiddenSize, TensorMetrics* metrics) {
    cudaEvent_t start, stop;
    cudaEventCreate(&start);
    cudaEventCreate(&stop);

    int threadsPerBlock = 256;
    dim3 grid( (hiddenSize + threadsPerBlock - 1) / threadsPerBlock, 1, batchSize);

    cudaEventRecord(start);
#ifdef __CUDACC__
    meanPoolingKernel<<<grid, threadsPerBlock>>>(d_Input, d_AttentionMask, d_Output, batchSize, seqLen, hiddenSize);
#else
    meanPoolingKernel(d_Input, d_AttentionMask, d_Output, batchSize, seqLen, hiddenSize);
#endif
    cudaEventRecord(stop);
    cudaEventSynchronize(stop);

    float milliseconds = 0;
    cudaEventElapsedTime(&milliseconds, start, stop);

    if (metrics != NULL) {
        metrics->compute_time_ms = milliseconds;
        metrics->active_kernels = 1;
    }

    cudaEventDestroy(start);
    cudaEventDestroy(stop);
}

EXPORT void ReluActivation_GPU(const float* d_Input, float* d_Output, int numElements, TensorMetrics* metrics) {
    cudaEvent_t start, stop;
    cudaEventCreate(&start);
    cudaEventCreate(&stop);

    int threadsPerBlock = 256;
    int blocksPerGrid = (numElements + threadsPerBlock - 1) / threadsPerBlock;

    cudaEventRecord(start);
#ifdef __CUDACC__
    reluKernel<<<blocksPerGrid, threadsPerBlock>>>(d_Input, d_Output, numElements);
#else
    reluKernel(d_Input, d_Output, numElements);
#endif
    cudaEventRecord(stop);
    cudaEventSynchronize(stop);

    float milliseconds = 0;
    cudaEventElapsedTime(&milliseconds, start, stop);

    if (metrics != NULL) {
        metrics->compute_time_ms = milliseconds;
        metrics->active_kernels = 1;
    }

    cudaEventDestroy(start);
    cudaEventDestroy(stop);
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
#ifdef __CUDACC__
    vectorAdd<<<blocksPerGrid, threadsPerBlock>>>(d_A, d_B, d_C, numElements);
#else
    // Mock launch for IntelliSense
    vectorAdd(d_A, d_B, d_C, numElements);
#endif
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

EXPORT void MatrixMultiply(const float* h_A, const float* h_B, float* h_C, int M, int N, int K, TensorMetrics* metrics) {
    size_t sizeA = M * K * sizeof(float);
    size_t sizeB = K * N * sizeof(float);
    size_t sizeC = M * N * sizeof(float);

    float *d_A = NULL, *d_B = NULL, *d_C = NULL;

    cudaEvent_t start, stop;
    cudaEventCreate(&start);
    cudaEventCreate(&stop);

    cudaMalloc((void**)&d_A, sizeA);
    cudaMalloc((void**)&d_B, sizeB);
    cudaMalloc((void**)&d_C, sizeC);

    cudaMemcpy(d_A, h_A, sizeA, cudaMemcpyHostToDevice);
    cudaMemcpy(d_B, h_B, sizeB, cudaMemcpyHostToDevice);

    dim3 threadsPerBlock(16, 16);
    dim3 blocksPerGrid((N + threadsPerBlock.x - 1) / threadsPerBlock.x,
                       (M + threadsPerBlock.y - 1) / threadsPerBlock.y);

    cudaEventRecord(start);
#ifdef __CUDACC__
    matMulKernel<<<blocksPerGrid, threadsPerBlock>>>(d_A, d_B, d_C, M, N, K);
#else
    matMulKernel(d_A, d_B, d_C, M, N, K);
#endif
    cudaEventRecord(stop);

    cudaMemcpy(h_C, d_C, sizeC, cudaMemcpyDeviceToHost);

    cudaEventSynchronize(stop);
    float milliseconds = 0;
    cudaEventElapsedTime(&milliseconds, start, stop);

    if (metrics != NULL) {
        metrics->compute_time_ms = milliseconds;
        size_t free_byte, total_byte;
        cudaMemGetInfo(&free_byte, &total_byte);
        metrics->memory_usage_mb = (float)(total_byte - free_byte) / 1024.0f / 1024.0f;
        metrics->active_kernels = 1;
    }

    cudaFree(d_A);
    cudaFree(d_B);
    cudaFree(d_C);
    cudaEventDestroy(start);
    cudaEventDestroy(stop);
}

EXPORT void MeanPooling(const float* h_Input, const long long* h_AttentionMask, float* h_Output, int batchSize, int seqLen, int hiddenSize, TensorMetrics* metrics) {
    size_t sizeInput = batchSize * seqLen * hiddenSize * sizeof(float);
    size_t sizeMask = batchSize * seqLen * sizeof(long long);
    size_t sizeOutput = batchSize * hiddenSize * sizeof(float);

    float *d_Input = NULL, *d_Output = NULL;
    long long *d_Mask = NULL;

    cudaEvent_t start, stop;
    cudaEventCreate(&start);
    cudaEventCreate(&stop);

    cudaMalloc((void**)&d_Input, sizeInput);
    cudaMalloc((void**)&d_Mask, sizeMask);
    cudaMalloc((void**)&d_Output, sizeOutput);

    cudaMemcpy(d_Input, h_Input, sizeInput, cudaMemcpyHostToDevice);
    cudaMemcpy(d_Mask, h_AttentionMask, sizeMask, cudaMemcpyHostToDevice);

    int threadsPerBlock = 256;
    dim3 grid( (hiddenSize + threadsPerBlock - 1) / threadsPerBlock, 1, batchSize);

    cudaEventRecord(start);
#ifdef __CUDACC__
    meanPoolingKernel<<<grid, threadsPerBlock>>>(d_Input, d_Mask, d_Output, batchSize, seqLen, hiddenSize);
#else
    meanPoolingKernel(d_Input, d_Mask, d_Output, batchSize, seqLen, hiddenSize);
#endif
    cudaEventRecord(stop);

    cudaMemcpy(h_Output, d_Output, sizeOutput, cudaMemcpyDeviceToHost);

    cudaEventSynchronize(stop);
    float milliseconds = 0;
    cudaEventElapsedTime(&milliseconds, start, stop);

    if (metrics != NULL) {
        metrics->compute_time_ms = milliseconds;
        size_t free_byte, total_byte;
        cudaMemGetInfo(&free_byte, &total_byte);
        metrics->memory_usage_mb = (float)(total_byte - free_byte) / 1024.0f / 1024.0f;
        metrics->active_kernels = 1;
    }

    cudaFree(d_Input);
    cudaFree(d_Mask);
    cudaFree(d_Output);
    cudaEventDestroy(start);
    cudaEventDestroy(stop);
}

EXPORT void ReluActivation(const float* h_Input, float* h_Output, int numElements, TensorMetrics* metrics) {
    size_t size = numElements * sizeof(float);
    float *d_Input = NULL, *d_Output = NULL;

    cudaEvent_t start, stop;
    cudaEventCreate(&start);
    cudaEventCreate(&stop);

    cudaMalloc((void**)&d_Input, size);
    cudaMalloc((void**)&d_Output, size);

    cudaMemcpy(d_Input, h_Input, size, cudaMemcpyHostToDevice);

    int threadsPerBlock = 256;
    int blocksPerGrid = (numElements + threadsPerBlock - 1) / threadsPerBlock;

    cudaEventRecord(start);
#ifdef __CUDACC__
    reluKernel<<<blocksPerGrid, threadsPerBlock>>>(d_Input, d_Output, numElements);
#else
    reluKernel(d_Input, d_Output, numElements);
#endif
    cudaEventRecord(stop);

    cudaMemcpy(h_Output, d_Output, size, cudaMemcpyDeviceToHost);

    cudaEventSynchronize(stop);
    float milliseconds = 0;
    cudaEventElapsedTime(&milliseconds, start, stop);

    if (metrics != NULL) {
        metrics->compute_time_ms = milliseconds;
        size_t free_byte, total_byte;
        cudaMemGetInfo(&free_byte, &total_byte);
        metrics->memory_usage_mb = (float)(total_byte - free_byte) / 1024.0f / 1024.0f;
        metrics->active_kernels = 1;
    }

    cudaFree(d_Input);
    cudaFree(d_Output);
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
