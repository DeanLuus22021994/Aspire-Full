# Base image with CUDA support
FROM nvidia/cuda:12.4.1-devel-ubuntu22.04 AS build

# Enable apt caching
RUN rm -f /etc/apt/apt.conf.d/docker-clean; echo 'Binary::apt::APT::Keep-Downloaded-Packages "true";' > /etc/apt/apt.conf.d/keep-cache

# Install build tools and dependencies
RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt,sharing=locked \
    apt-get update && apt-get install -y \
    build-essential \
    wget

# Install CMake 3.29 (required for C++20/CUDA support)
RUN wget https://github.com/Kitware/CMake/releases/download/v3.29.0/cmake-3.29.0-linux-x86_64.sh \
    && chmod +x cmake-3.29.0-linux-x86_64.sh \
    && ./cmake-3.29.0-linux-x86_64.sh --skip-license --prefix=/usr/local \
    && rm cmake-3.29.0-linux-x86_64.sh

WORKDIR /src

# Copy source code
COPY . .

# Build
RUN --mount=type=cache,target=/src/build \
    mkdir -p build && cd build && cmake .. && make

# Output stage (optional, for extracting artifacts)
FROM scratch AS export
COPY --from=build /src/build/libAspireFullNative.so /
