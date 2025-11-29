FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Install .NET 10 Preview SDK
RUN apt-get update && apt-get install -y wget && \
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 10.0 --quality preview --install-dir /usr/share/dotnet
ENV PATH="/usr/share/dotnet:$PATH"

COPY ["Aspire-Full.slnf", "."]
COPY ["Aspire-Full.slnx", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["NuGet.config", "."]

COPY ["Aspire-Full.WebAssembly/", "Aspire-Full.WebAssembly/"]
COPY ["Aspire-Full.ServiceDefaults/", "Aspire-Full.ServiceDefaults/"]
COPY ["Aspire-Full.Tensor/", "Aspire-Full.Tensor/"]

WORKDIR /src/Aspire-Full.WebAssembly
RUN dotnet restore
RUN dotnet publish "Aspire-Full.WebAssembly.csproj" -c Release -o /app/publish

FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
COPY --from=build /app/publish/wwwroot .
COPY ["Aspire-Full.WebAssembly/nginx.conf", "/etc/nginx/conf.d/default.conf"]
EXPOSE 80
