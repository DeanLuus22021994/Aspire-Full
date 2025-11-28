# VS Code Extension Containers

This directory hosts self-contained Docker build contexts for VS Code extensions that we want cached locally. Each service downloads the latest VSIX from the marketplace using `fetch_extension.py` and stores it inside a named Docker volume so the main devcontainer can install the extension without waiting on the network.

## Available containers

| Extension | Folder | Volume |
| --- | --- | --- |
| ms-windows-ai-studio.windows-ai-studio | `windows-ai-studio/` | `aspire_windows_ai_studio_extension_cache` |
| ms-azuretools.vscode-azure-github-copilot | `azure-github-copilot/` | `aspire_azure_github_copilot_extension_cache` |

## Usage

```bash
# From the repository root
docker compose -f .vscode/extensions/docker-compose.extensions.yml up -d

# Copy the cached VSIX into your devcontainer/host
docker compose -f .vscode/extensions/docker-compose.extensions.yml cp \
  windows-ai-studio-extension:/opt/extensions/ms-windows-ai-studio.windows-ai-studio/ms-windows-ai-studio.windows-ai-studio.vsix \
  ./artifacts/
```

Point VS Code at the `artifacts` directory (or mount the named volume) to install the extension offline.
