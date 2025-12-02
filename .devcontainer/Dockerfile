# =============================================================================
# Aspire-Full Lightweight DevContainer - GPU-Only Compute
# CUDA/CU Runtime | 2GB Host RAM Max | WASM Model Runner
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS base

# =============================================================================
# MEMORY CONSTRAINTS: 2GB Host RAM Max
# All tensor/embedding compute offloaded to GPU via CUDA
# =============================================================================
ENV DOTNET_GCHeapHardLimit=1610612736 \
    DOTNET_GCHeapHardLimitPercent=0x50 \
    DOTNET_gcServer=0 \
    DOTNET_GCConserveMemory=9 \
    DOTNET_TieredCompilation=1 \
    DOTNET_ReadyToRun=0

# =============================================================================
# GPU COMPUTE: CUDA/CU Only - No CPU Fallback
# =============================================================================
ENV ASPIRE_COMPUTE_MODE=Gpu \
    ASPIRE_OFFLOAD_STRATEGY=Full \
    ASPIRE_FALLBACK_TO_CPU=false \
    CUDA_VISIBLE_DEVICES=all \
    NVIDIA_VISIBLE_DEVICES=all \
    NVIDIA_DRIVER_CAPABILITIES=compute,utility

# Install WASM workload and minimal tools
RUN dotnet workload install wasm-tools \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        git \
        curl \
        jq \
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
# Runtime Configuration
# =============================================================================
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    NUGET_XMLDOC_MODE=skip \
    ASPIRE_ALLOW_UNSECURED_TRANSPORT=true

# Trust HTTPS dev certificates
RUN dotnet dev-certs https --trust 2>/dev/null || true

# Healthcheck for container orchestration
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:18888/health || exit 1

CMD ["sleep", "infinity"]
