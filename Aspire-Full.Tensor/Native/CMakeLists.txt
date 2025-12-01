cmake_minimum_required(VERSION 3.28)
project(AspireFullNative LANGUAGES CXX CUDA)

set(CMAKE_CUDA_STANDARD 20)
set(CMAKE_CUDA_STANDARD_REQUIRED ON)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(NOT DEFINED CMAKE_CUDA_COMPILER)
    set(CUDA_TOOLKIT_ROOT_DIR "C:/Program Files/NVIDIA GPU Computing Toolkit/CUDA/v13.0")
    if(EXISTS "${CUDA_TOOLKIT_ROOT_DIR}/bin/nvcc.exe")
        set(CMAKE_CUDA_COMPILER "${CUDA_TOOLKIT_ROOT_DIR}/bin/nvcc.exe")
    endif()
endif()

# Target NVIDIA GeForce RTX 3050 (Compute Capability 8.6)
if(NOT DEFINED CMAKE_CUDA_ARCHITECTURES)
    set(CMAKE_CUDA_ARCHITECTURES 86)
endif()

# Include directories
include_directories(include)

# Define build macro
add_compile_definitions(ASPIRE_NATIVE_BUILD)

# Add library with source from src/
add_library(AspireFullNative SHARED src/tensor_ops.cpp)

# Force CMake to treat the .cpp file as CUDA source so nvcc is used
set_source_files_properties(src/tensor_ops.cpp PROPERTIES LANGUAGE CUDA)

set_target_properties(AspireFullNative PROPERTIES
    CUDA_SEPARABLE_COMPILATION ON
    POSITION_INDEPENDENT_CODE ON
)

if(WIN32)
    set_target_properties(AspireFullNative PROPERTIES WINDOWS_EXPORT_ALL_SYMBOLS ON)
endif()

if(CMAKE_BUILD_TYPE STREQUAL "Release")
    target_compile_options(AspireFullNative PRIVATE $<$<COMPILE_LANGUAGE:CUDA>:-O3 --use_fast_math -Xptxas=-O3>)
endif()

