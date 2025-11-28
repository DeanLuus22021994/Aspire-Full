# Windows AI Studio Extension Container

This container pre-fetches the `ms-windows-ai-studio.windows-ai-studio` VS Code extension into a persistent volume so the marketplace payload is always available locally.

- **Image base**: `mcr.microsoft.com/devcontainers/base:jammy`
- **Cache path**: `/opt/extensions/ms-windows-ai-studio.windows-ai-studio`
- **Named volume**: `aspire_windows_ai_studio_extension_cache`

Use `docker compose -f .vscode/extensions/docker-compose.extensions.yml up -d windows-ai-studio-extension` to build and launch the service. The downloaded VSIX is stored inside the named volume and can be mounted into other development containers or copied directly into the VS Code extensions directory for offline installs.
