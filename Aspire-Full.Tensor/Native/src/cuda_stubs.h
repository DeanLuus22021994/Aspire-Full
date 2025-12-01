#pragma once

#ifndef __CUDACC__
// #include <stdlib.h> // Removed to avoid dependency on host headers
typedef unsigned long long size_t;

#define __global__
#define __device__
#define __host__

typedef int cudaError_t;
typedef int cudaEvent_t;

#define cudaSuccess 0
#define cudaMemcpyHostToDevice 1
#define cudaMemcpyDeviceToHost 2

#ifndef NULL
#define NULL 0
#endif

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

#endif // __CUDACC__
