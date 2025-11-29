# Stage 1: Build Native Library
FROM nvidia/cuda:12.4.1-devel-ubuntu22.04 AS native-build
WORKDIR /src

# Enable apt caching
RUN rm -f /etc/apt/apt.conf.d/docker-clean; echo 'Binary::apt::APT::Keep-Downloaded-Packages "true";' > /etc/apt/apt.conf.d/keep-cache

# Install CMake 3.29 (required for C++20/CUDA support) and build tools
RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt,sharing=locked \
    apt-get update && apt-get install -y build-essential wget && \
    wget https://github.com/Kitware/CMake/releases/download/v3.29.0/cmake-3.29.0-linux-x86_64.sh && \
    chmod +x cmake-3.29.0-linux-x86_64.sh && \
    ./cmake-3.29.0-linux-x86_64.sh --skip-license --prefix=/usr/local && \
    rm cmake-3.29.0-linux-x86_64.sh

# Copy Native Project
COPY ["Aspire-Full.Native/", "Aspire-Full.Native/"]

# Build Native Library
WORKDIR /src/Aspire-Full.Native
RUN --mount=type=cache,target=/src/Aspire-Full.Native/build \
    mkdir -p build && cd build && cmake .. && make

# Stage 2: Build .NET Application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-build
WORKDIR /src

# Enable apt caching
RUN rm -f /etc/apt/apt.conf.d/docker-clean; echo 'Binary::apt::APT::Keep-Downloaded-Packages "true";' > /etc/apt/apt.conf.d/keep-cache

# Install .NET 10 Preview SDK
RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt,sharing=locked \
    apt-get update && apt-get install -y wget && \
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 10.0 --quality preview --install-dir /usr/share/dotnet
ENV PATH="/usr/share/dotnet:$PATH"

# Copy Solution and Config Files
COPY ["Aspire-Full.slnf", "."]
COPY ["Aspire-Full.slnx", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["NuGet.config", "."]

# Copy Projects
COPY ["Aspire-Full.Api/", "Aspire-Full.Api/"]
COPY ["Aspire-Full.ServiceDefaults/", "Aspire-Full.ServiceDefaults/"]
COPY ["Aspire-Full.DockerRegistry/", "Aspire-Full.DockerRegistry/"]
COPY ["Aspire-Full.Connectors/", "Aspire-Full.Connectors/"]
COPY ["Aspire-Full.Tensor/", "Aspire-Full.Tensor/"]
COPY ["Aspire-Full.VectorStore/", "Aspire-Full.VectorStore/"]
COPY ["Aspire-Full.Qdrant/", "Aspire-Full.Qdrant/"]

# Restore and Publish
WORKDIR /src/Aspire-Full.Api
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "Aspire-Full.Api.csproj" -c Release -r linux-x64 --self-contained true -o /app/publish /p:UseAppHost=true

# Stage 3: Runtime
FROM nvidia/cuda:12.4.1-runtime-ubuntu22.04 AS final
WORKDIR /app

# Enable apt caching
RUN rm -f /etc/apt/apt.conf.d/docker-clean; echo 'Binary::apt::APT::Keep-Downloaded-Packages "true";' > /etc/apt/apt.conf.d/keep-cache

# Install .NET Runtime Dependencies (Self-contained app needs these)
RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt,sharing=locked \
    apt-get update && apt-get install -y libicu-dev libssl-dev

# Copy Native Library
COPY --from=native-build /src/Aspire-Full.Native/build/libAspireFullNative.so /app/

# Copy Published Application
COPY --from=dotnet-build /app/publish .

# Configure Environment
ENV LD_LIBRARY_PATH="/app:$LD_LIBRARY_PATH"
ENV ASPNETCORE_URLS="http://+:8080"

ENTRYPOINT ["./Aspire-Full.Api"]
