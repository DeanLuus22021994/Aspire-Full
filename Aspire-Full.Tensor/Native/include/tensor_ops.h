#pragma once

#ifdef ASPIRE_NATIVE_BUILD
#if defined(_MSC_VER)
#define EXPORT extern "C" __declspec(dllexport)
#else
#define EXPORT extern "C" __attribute__((visibility("default")))
#endif
#else
// Editor/IntelliSense mode
#define EXPORT extern "C"
#endif

struct TensorMetrics {
    float compute_time_ms;
    float memory_usage_mb;
    int active_kernels;
};

EXPORT int InitTensorContext();

// Memory Management
EXPORT float* AllocateDeviceMemory(size_t sizeBytes);
EXPORT void FreeDeviceMemory(float* d_ptr);
EXPORT void CopyToDevice(float* d_dst, const float* h_src, size_t sizeBytes);
EXPORT void CopyToHost(float* h_dst, const float* d_src, size_t sizeBytes);

EXPORT long long* AllocateDeviceMemoryLong(size_t sizeBytes);
EXPORT void FreeDeviceMemoryLong(long long* d_ptr);
EXPORT void CopyToDeviceLong(long long* d_dst, const long long* h_src, size_t sizeBytes);

// Compute Operations (Inputs/Outputs are DEVICE pointers)
EXPORT void MatrixMultiply_GPU(const float* d_A, const float* d_B, float* d_C, int M, int N, int K, TensorMetrics* metrics);
EXPORT void MeanPooling_GPU(const float* d_Input, const long long* d_AttentionMask, float* d_Output, int batchSize, int seqLen, int hiddenSize, TensorMetrics* metrics);
EXPORT void ReluActivation_GPU(const float* d_Input, float* d_Output, int numElements, TensorMetrics* metrics);

// Legacy/Convenience (Host pointers) - kept for backward compatibility if needed, or removed.
// Removing to enforce new pattern.

