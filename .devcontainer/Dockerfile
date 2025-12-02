# =============================================================================
# Aspire-Full Lightweight DevContainer
# Official .NET 10 SDK with WASM workload - Back to Basics
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS base

# Install WASM workload and essential tools
RUN dotnet workload install wasm-tools wasm-experimental \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        git \
        curl \
        jq \
        postgresql-client \
        redis-tools \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
ARG USERNAME=vscode
ARG USER_UID=1000
ARG USER_GID=$USER_UID

RUN groupadd --gid $USER_GID $USERNAME \
    && useradd --uid $USER_UID --gid $USER_GID -m $USERNAME \
    && apt-get update \
    && apt-get install -y sudo \
    && echo $USERNAME ALL=\(root\) NOPASSWD:ALL > /etc/sudoers.d/$USERNAME \
    && chmod 0440 /etc/sudoers.d/$USERNAME \
    && rm -rf /var/lib/apt/lists/*

# Install Node.js for frontend tooling (minimal)
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y nodejs \
    && npm install -g npm@latest \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /workspace

# Switch to non-root user
USER $USERNAME

# Configure .NET environment
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    NUGET_XMLDOC_MODE=skip

# Trust HTTPS dev certificates
RUN dotnet dev-certs https --trust 2>/dev/null || true

CMD ["sleep", "infinity"]
