# DevContainer Setup

## Overview

The devcontainer provides a fully isolated, reproducible development environment with all tools pre-configured.

## Features

- **Base Image**: `mcr.microsoft.com/devcontainers/base:bookworm`
- **No Host Mounts**: All data stored in named Docker volumes
- **Pre-warmed Caches**: NuGet packages pre-cached for faster builds
- **Full Toolchain**: .NET 10, Docker CLI, GitHub CLI, Aspire CLI
- **Tensor Acceleration**: Devcontainer requires NVIDIA runtime + devices; startup scripts fail fast if CUDA hardware is missing

## Configuration Files

```
.devcontainer/
├── devcontainer.json    # VS Code configuration
├── docker-compose.yml   # Service definitions
├── Dockerfile           # Container image
└── scripts/
  ├── post_create.py   # Initial setup (Python 3.14 free-threaded ready)
  └── post_start.py    # Startup checks (Python 3.14 free-threaded ready)
```

## .NET Aspire Integration

- `Aspire-Full.DevContainer` is a dedicated .NET 10 project that ships the Dockerfile, compose file, and bootstrap scripts. It also exposes `AddDevContainer()` so the AppHost can register the container declaratively.
- `Aspire-Full/AppHost.cs` now calls `builder.AddDevContainer("aspire-network")`, guaranteeing all resource metadata (volumes, env vars, Python 3.14 free-threaded pins) stays centralized.
- The devcontainer depends on two additional Aspire resources:
  - `docker` (Docker-in-Docker daemon with the `aspire-docker-*` volumes)
  - `aspire-dashboard` (dashboard/MCP endpoints mapped through `WithHttpEndpoint`)
- Running `dotnet run --project Aspire-Full` starts the API, web app, backing stores, the Docker daemon, and the devcontainer itself with the same named volumes defined in `.devcontainer/docker-compose.yml`.
- All bootstrap scripts (`post_create.py`, `post_start.py`) now resolve the workspace via `WORKSPACE_FOLDER`/`cwd`, so no absolute `/workspace` assumptions leak outside the container runtime.

## Services

### DevContainer Service

Main development environment container.

```yaml
devcontainer:
  build:
    context: .
    dockerfile: Dockerfile
  volumes:
    - aspire-nuget-cache:/home/vscode/.nuget
    - aspire-dotnet-tools:/home/vscode/.dotnet/tools
    - aspire-aspire-cli:/home/vscode/.aspire
    - aspire-vscode-extensions:/home/vscode/.vscode-server/extensions
    - aspire-workspace:/workspace
```

### Aspire Dashboard Service

Telemetry and monitoring dashboard.

```yaml
aspire-dashboard:
  image: mcr.microsoft.com/dotnet/aspire-dashboard:latest
  ports:
    - "18888:18888"  # Dashboard UI
    - "18889:18889"  # OTLP endpoint
```

## Installed Tools

| Tool | Command | Version |
|------|---------|---------|
| .NET SDK | `dotnet` | 10.0.100 |
| Aspire CLI | `aspire` | 13.0.1 |
| Docker CLI | `docker` | Latest |
| GitHub CLI | `gh` | Latest |
| Entity Framework | `dotnet-ef` | Latest |

## GitHub CLI Extensions

Pre-installed in the container:

- `gh-copilot` - AI assistance
- `gh-models` - GitHub Models API
- `gh-act` - Local Actions runner
- `gh-dash` - Terminal dashboard
- `gh-sbom` - SBOM generation
- `gh-projects` - Project management
- `gh-actions-cache` - Cache management
- `gh-aw` - Agentic workflows

## Volume Management

### Creating Volumes

```bash
docker volume create aspire-nuget-cache
docker volume create aspire-dotnet-tools
docker volume create aspire-aspire-cli
docker volume create aspire-vscode-extensions
docker volume create aspire-workspace
docker volume create aspire-dashboard-data
```

### Listing Volumes

```bash
docker volume ls --filter name=aspire
```

### Cleaning Up

```bash
# Remove all aspire volumes
docker volume ls --filter name=aspire -q | xargs docker volume rm
```

## Rebuilding

To rebuild the devcontainer:

1. In VS Code: `Ctrl+Shift+P` → "Dev Containers: Rebuild Container"
2. Or from terminal:
```bash
cd .devcontainer
docker compose build --no-cache
```

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker compose logs devcontainer

# Verify volumes exist
docker volume ls --filter name=aspire
```

### Network Issues

```bash
# Recreate network
docker network rm aspire-network
docker network create aspire-network
```

### Permission Issues

The container runs as user `vscode` (UID 1000). Ensure volumes have correct permissions:

```bash
docker exec -it devcontainer-devcontainer-1 ls -la /home/vscode
```
