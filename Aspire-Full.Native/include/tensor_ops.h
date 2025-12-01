#pragma once

#if defined(_WIN32)
#define EXPORT extern "C" __declspec(dllexport)
#else
#define EXPORT extern "C" __attribute__((visibility("default")))
#endif

struct TensorMetrics {
    float compute_time_ms;
    float memory_usage_mb;
    int active_kernels;
};

EXPORT int InitTensorContext();
EXPORT void ComputeTensorOp(const float* h_A, const float* h_B, float* h_C, int numElements, TensorMetrics* metrics);
EXPORT int ValidateTensorContent(const float* h_Data, int numElements, float threshold, TensorMetrics* metrics);
