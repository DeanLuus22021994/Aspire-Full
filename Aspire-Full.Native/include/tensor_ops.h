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
EXPORT void ComputeTensorOp(const float* h_A, const float* h_B, float* h_C, int numElements, TensorMetrics* metrics);
EXPORT int ValidateTensorContent(const float* h_Data, int numElements, float threshold, TensorMetrics* metrics);

// Enhanced Operations
EXPORT void MatrixMultiply(const float* h_A, const float* h_B, float* h_C, int M, int N, int K, TensorMetrics* metrics);
EXPORT void MeanPooling(const float* h_Input, const long long* h_AttentionMask, float* h_Output, int batchSize, int seqLen, int hiddenSize, TensorMetrics* metrics);
EXPORT void ReluActivation(const float* h_Input, float* h_Output, int numElements, TensorMetrics* metrics);
