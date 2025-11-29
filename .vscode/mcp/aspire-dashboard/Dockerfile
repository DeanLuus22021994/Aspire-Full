# Stage 1: Get the dashboard binaries
FROM mcr.microsoft.com/dotnet/aspire-dashboard:latest AS source

# Stage 2: Build the runtime image with NVIDIA support
FROM nvidia/cuda:12.4.1-base-ubuntu22.04

# Install dependencies and .NET Runtime
# We install .NET 9.0 Runtime as it is the current GA version for Aspire
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget \
    ca-certificates \
    && wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
    aspnetcore-runtime-9.0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=source /app /app

# Configuration
ENV DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
    DASHBOARD__OTLP__AUTHMODE=Unsecured \
    DASHBOARD__FRONTEND__AUTHMODE=Unsecured \
    DASHBOARD__RESOURCESERVICE__AUTHMODE=Unsecured \
    ASPIRE_DASHBOARD_MCP_ENDPOINT_URL=http://0.0.0.0:16036 \
    ASPIRE_ALLOW_UNSECURED_TRANSPORT=true \
    # NVIDIA Configuration
    NVIDIA_VISIBLE_DEVICES=all \
    NVIDIA_DRIVER_CAPABILITIES=compute,utility

EXPOSE 18888 18889 16036
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "Microsoft.Aspire.Dashboard.Web.dll"]
