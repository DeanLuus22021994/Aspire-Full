# =============================================================================
# Aspire-Full High-Performance DevContainer - GPU Tensor Compute
# CUDA/CU Runtime | 9GB Host RAM | Named Volume Persistence | Zero Pollution
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS base

# =============================================================================
# MEMORY CONFIGURATION: 9GB Host RAM (9,663,676,416 bytes)
# Optimized for tensor compute with GPU offload and host memory for batching
# =============================================================================
ENV DOTNET_GCHeapHardLimit=7516192768 \
    DOTNET_GCHeapHardLimitPercent=0x52 \
    DOTNET_gcServer=1 \
    DOTNET_GCConserveMemory=5 \
    DOTNET_TieredCompilation=1 \
    DOTNET_TieredPGO=1 \
    DOTNET_ReadyToRun=1 \
    DOTNET_TC_QuickJitForLoops=1

# =============================================================================
# GPU COMPUTE: CUDA/CU with Hybrid Fallback for Maximum Throughput
# =============================================================================
# Python 3.15t Free-Threading environment
ENV PYTHON_GIL=0 \
    PYTHON3_15T=/usr/local/python3.15t/bin/python3.15t \
    ASPIRE_COMPUTE_MODE=gpu \
    ASPIRE_OFFLOAD_STRATEGY=Full \
    ASPIRE_FALLBACK_TO_CPU=false \
    ASPIRE_MAX_BATCH_SIZE=64 \
    ASPIRE_BATCH_TIMEOUT_MS=25 \
    ASPIRE_GPU_MEMORY_FRACTION=0.90 \
    ASPIRE_ENABLE_DYNAMIC_BATCHING=true \
    ASPIRE_ENABLE_UNIFIED_MEMORY=true \
    ASPIRE_PIN_HOST_MEMORY=true \
    CUDA_VISIBLE_DEVICES=all \
    NVIDIA_VISIBLE_DEVICES=all \
    NVIDIA_DRIVER_CAPABILITIES=compute,utility,video \
    NVIDIA_REQUIRE_CUDA="cuda>=12.0" \
    CUDA_CACHE_DISABLE=0 \
    CUDA_FORCE_PTX_JIT=0

# =============================================================================
# BAKED TOOLCHAIN: Everything needed for builds - no runtime downloads
# =============================================================================
RUN dotnet workload install wasm-tools aspire \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        # Essential dev tools
        git \
        curl \
        wget \
        jq \
        htop \
        nvtop \
        unzip \
        # Build essentials
        build-essential \
        cmake \
        ninja-build \
        # GPU/Tensor compute libs
        libgomp1 \
        libnuma1 \
        libopenblas0 \
        libopenblas-dev \
        # Python 3.15t Free-Threaded (64-bit) for AI/ML
        # Note: Install via deadsnakes PPA or build from source
        software-properties-common \
        # Node.js (for React frontend)
        nodejs \
        npm \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# =============================================================================
# Python 3.15t Free-Threaded (GIL disabled) + CuPy Hyper-Virtual GPU
# =============================================================================
# Build Python 3.15 with --disable-gil for true free-threading
RUN apt-get update && apt-get install -y --no-install-recommends \
        libffi-dev \
        libssl-dev \
        zlib1g-dev \
        libbz2-dev \
        libreadline-dev \
        libsqlite3-dev \
        libncurses5-dev \
        libncursesw5-dev \
        xz-utils \
        tk-dev \
        liblzma-dev \
    && cd /tmp \
    && wget https://www.python.org/ftp/python/3.15.0/Python-3.15.0a2.tar.xz \
    && tar -xf Python-3.15.0a2.tar.xz \
    && cd Python-3.15.0a2 \
    && ./configure --enable-optimizations --disable-gil --prefix=/usr/local/python3.15t \
    && make -j$(nproc) \
    && make install \
    && ln -sf /usr/local/python3.15t/bin/python3.15t /usr/local/bin/python3.15t \
    && ln -sf /usr/local/python3.15t/bin/pip3.15 /usr/local/bin/pip3.15t \
    && rm -rf /tmp/Python-3.15.0a2* \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# Install uv (fast Python package manager) and ruff (linter)
RUN curl -LsSf https://astral.sh/uv/install.sh | sh \
    && curl -LsSf https://astral.sh/ruff/install.sh | sh

# Install core Python packages for GPU compute
# Note: CuPy requires CUDA toolkit at runtime - install it separately if needed
# Using pip with proper quoting for version specifiers
RUN /usr/local/bin/pip3.15t install --no-cache-dir \
        "numpy>=2.0.0" \
    && /usr/local/bin/pip3.15t install --no-cache-dir \
        "cupy-cuda12x>=13.0.0" || echo "⚠️ CuPy installation skipped - will install at runtime with GPU"

# =============================================================================
# VOLUME MOUNT POINTS: Named volumes for all persistent/cache data
# These directories will be mounted as Docker named volumes
# =============================================================================
ENV NUGET_PACKAGES=/volumes/nuget \
    DOTNET_CLI_HOME=/volumes/dotnet-cli \
    npm_config_cache=/volumes/npm \
    UV_CACHE_DIR=/volumes/uv \
    PIP_CACHE_DIR=/volumes/pip \
    CUDA_CACHE_PATH=/volumes/cuda-cache \
    ASPIRE_MODEL_CACHE_DIR=/volumes/models \
    QDRANT_STORAGE=/volumes/qdrant \
    ASPIRE_TENSOR_CACHE=/volumes/tensor-cache \
    ASPIRE_LOGS_DIR=/volumes/logs \
    ASPIRE_TMP_DIR=/volumes/tmp

# Create non-root user - handle existing UID/GID gracefully
ARG USERNAME=vscode
ARG USER_UID=1000
ARG USER_GID=$USER_UID

RUN if getent group $USER_GID >/dev/null 2>&1; then \
        groupmod -n $USERNAME $(getent group $USER_GID | cut -d: -f1) 2>/dev/null || true; \
    else \
        groupadd --gid $USER_GID $USERNAME; \
    fi \
    && if getent passwd $USER_UID >/dev/null 2>&1; then \
        existing_user=$(getent passwd $USER_UID | cut -d: -f1); \
        usermod -l $USERNAME -d /home/$USERNAME -m $existing_user 2>/dev/null || true; \
    else \
        useradd --uid $USER_UID --gid $USER_GID -m $USERNAME; \
    fi \
    && mkdir -p /etc/sudoers.d \
    && echo "$USERNAME ALL=(root) NOPASSWD:ALL" > /etc/sudoers.d/$USERNAME \
    && chmod 0440 /etc/sudoers.d/$USERNAME

# Create all volume mount points with correct ownership
RUN mkdir -p \
        /volumes/nuget \
        /volumes/dotnet-cli \
        /volumes/npm \
        /volumes/uv \
        /volumes/pip \
        /volumes/cuda-cache \
        /volumes/models \
        /volumes/qdrant \
        /volumes/tensor-cache \
        /volumes/logs \
        /volumes/tmp \
        /volumes/artifacts \
        /volumes/testresults \
    && chown -R $USER_UID:$USER_GID /volumes

# Add cargo/uv to PATH for vscode user
ENV PATH="/root/.local/bin:/home/$USERNAME/.local/bin:$PATH"

WORKDIR /workspace
USER $USERNAME

# =============================================================================
# Runtime Configuration - Optimized for Tensor Compute
# =============================================================================
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    NUGET_XMLDOC_MODE=skip \
    ASPIRE_ALLOW_UNSECURED_TRANSPORT=true \
    # Tensor/BLAS optimizations
    OMP_NUM_THREADS=8 \
    MKL_NUM_THREADS=8 \
    OPENBLAS_NUM_THREADS=8 \
    GOTO_NUM_THREADS=8 \
    OMP_WAIT_POLICY=ACTIVE \
    OMP_DYNAMIC=false \
    # Memory pool settings
    ASPIRE_TENSOR_POOL_MAX_BUFFERS=32 \
    ASPIRE_TENSOR_POOL_DEFAULT_SIZE=134217728 \
    ASPIRE_MAX_CACHED_MODELS=20

# Trust HTTPS dev certificates
RUN dotnet dev-certs https --trust 2>/dev/null || true

# Healthcheck for container orchestration
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:18888/health || exit 1

CMD ["sleep", "infinity"]
