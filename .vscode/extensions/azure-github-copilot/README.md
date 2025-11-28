# Azure GitHub Copilot Extension Container

This container pre-fetches the `ms-azuretools.vscode-azure-github-copilot` VS Code extension so we always have the VSIX locally with no marketplace latency.

- **Image base**: `mcr.microsoft.com/devcontainers/base:jammy`
- **Cache path**: `/opt/extensions/ms-azuretools.vscode-azure-github-copilot`
- **Named volume**: `aspire_azure_github_copilot_extension_cache`

Run `docker compose -f .vscode/extensions/docker-compose.extensions.yml up -d azure-github-copilot-extension` to build and keep the cache container warm. Mount the resulting volume into the main devcontainer or copy the VSIX from the volume to install the extension offline.
