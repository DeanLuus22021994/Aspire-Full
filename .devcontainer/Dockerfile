# =============================================================================
# Aspire-Full High-Performance DevContainer - GPU Tensor Compute
# CUDA/CU Runtime | 9GB Host RAM | Optimized Tensor Operations
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
ENV ASPIRE_COMPUTE_MODE=Hybrid \
    ASPIRE_OFFLOAD_STRATEGY=Full \
    ASPIRE_FALLBACK_TO_CPU=true \
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

# Install WASM workload, tensor compute dependencies, and dev tools
RUN dotnet workload install wasm-tools aspire \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        git \
        curl \
        jq \
        htop \
        nvtop \
        libgomp1 \
        libnuma1 \
        libopenblas0 \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# Create non-root user (minimal) - handle existing UID/GID gracefully
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
    # Model cache settings
    ASPIRE_MODEL_CACHE_DIR=/shared/models \
    ASPIRE_MAX_CACHED_MODELS=20

# Trust HTTPS dev certificates
RUN dotnet dev-certs https --trust 2>/dev/null || true

# Healthcheck for container orchestration
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:18888/health || exit 1

CMD ["sleep", "infinity"]
