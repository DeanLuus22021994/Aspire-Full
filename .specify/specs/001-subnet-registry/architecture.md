# Architecture: Subnet & Registry

## Components

### 1. Internal Registry (`registry:2`)
- **Image**: `registry:2`
- **Port**: 5000 (Internal/Host)
- **Storage**: Named volume `aspire-registry-data` for persistence.
- **Network**: `aspire-network`.

### 2. Agent Container Image (`aspire-agents:latest`)
- **Base**: `python:3.14-rc-slim` (or similar available tag).
- **Env**: `PYTHON_GIL=0` (Free-threading).
- **Build Process**:
    1. Install system deps (`libportaudio2`, `ffmpeg`, `playwright-deps`).
    2. Install python deps via `uv`.
    3. Run `python -m aspire_agents.scripts.download_models` to cache models.
    4. Run `playwright install` to cache browsers.
- **Runtime**: `uvicorn` server.

### 3. Orchestration (`AppHost.cs`)
- **Registry Resource**: Added as a container resource.
- **Agent Resource**: Changed from `AddExecutable` to `AddContainer`.
    - Image: `localhost:5000/aspire-agents:latest`.
    - Build: Triggered via script or external task before `AppHost` run (or integrated if possible).

## Data Flow
1. **Provisioning**: `scripts/provision.ps1` builds `Dockerfile.agent` -> Pushes to `localhost:5000`.
2. **Startup**: `AppHost` starts -> Pulls from `localhost:5000` -> Starts Agent Container.
3. **Runtime**: Agent Container uses local GPU and pre-loaded models.
