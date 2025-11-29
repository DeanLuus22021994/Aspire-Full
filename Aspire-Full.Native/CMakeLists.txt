cmake_minimum_required(VERSION 3.28)
project(AspireFullNative LANGUAGES CXX CUDA)

set(CMAKE_CUDA_STANDARD 20)
set(CMAKE_CUDA_STANDARD_REQUIRED ON)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Target NVIDIA GeForce RTX 3050 (Compute Capability 8.6)
set(CMAKE_CUDA_ARCHITECTURES 86)

add_library(AspireFullNative SHARED tensor_ops.cu)

set_target_properties(AspireFullNative PROPERTIES
    CUDA_SEPARABLE_COMPILATION ON
    POSITION_INDEPENDENT_CODE ON
)

if(WIN32)
    set_target_properties(AspireFullNative PROPERTIES WINDOWS_EXPORT_ALL_SYMBOLS ON)
endif()

if(CMAKE_BUILD_TYPE STREQUAL "Release")
    target_compile_options(AspireFullNative PRIVATE $<$<COMPILE_LANGUAGE:CUDA>:-O3 --use_fast_math -Xptxas -O3>)
endif()

