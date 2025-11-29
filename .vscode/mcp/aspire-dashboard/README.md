# Aspire Dashboard MCP Container

Wraps the upstream `mcr.microsoft.com/dotnet/aspire-dashboard` image with the unsecured settings we use for local development. 

**Enhanced Features:**
- **NVIDIA Runtime Support**: Based on `nvidia/cuda:12.4.1-base-ubuntu22.04` to include NVIDIA runtime binaries and capabilities.
- **Persistence**: Data persists under the named Docker volume mounted at `/app/data`.

The image exposes:
- UI: `18888`
- OTLP endpoint: `18889`
- MCP endpoint: `16036`

