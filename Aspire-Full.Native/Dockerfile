# Base image with CUDA support
FROM nvidia/cuda:12.4.1-devel-ubuntu22.04 AS build

# Install build tools and dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    wget \
    && rm -rf /var/lib/apt/lists/*

# Install CMake 3.29 (required for C++20/CUDA support)
RUN wget https://github.com/Kitware/CMake/releases/download/v3.29.0/cmake-3.29.0-linux-x86_64.sh \
    && chmod +x cmake-3.29.0-linux-x86_64.sh \
    && ./cmake-3.29.0-linux-x86_64.sh --skip-license --prefix=/usr/local \
    && rm cmake-3.29.0-linux-x86_64.sh

WORKDIR /src

# Copy source code
COPY . .

# Build
RUN mkdir build && cd build && cmake .. && make

# Output stage (optional, for extracting artifacts)
FROM scratch AS export
COPY --from=build /src/build/libAspireFullNative.so /
