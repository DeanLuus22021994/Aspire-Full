# Aspire Dashboard MCP Container

Wraps the upstream `mcr.microsoft.com/dotnet/aspire-dashboard` image with the unsecured settings we use for local development. The image exposes:

- UI: `18888`
- OTLP endpoint: `18889`
- MCP endpoint: `16036`

Data persists under the named Docker volume mounted at `/app/data`.
