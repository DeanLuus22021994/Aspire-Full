# VS Code Extension Containers

This directory hosts self-contained Docker build contexts for VS Code extensions that we want cached locally. Each service downloads the latest VSIX from the marketplace using `fetch_extension.py` and stores it inside a named Docker volume so the main devcontainer can install the extension without waiting on the network.

## Available containers

| Extension | Folder | Volume |
| --- | --- | --- |
| ms-windows-ai-studio.windows-ai-studio | `ms-windows-ai-studio.windows-ai-studio/` | `aspire_ms_windows_ai_studio_windows_ai_studio_extension_cache` |
| ms-azuretools.vscode-azure-github-copilot | `ms-azuretools.vscode-azure-github-copilot/` | `aspire_ms_azuretools_vscode_azure_github_copilot_extension_cache` |
| ms-dotnettools.csharp | `ms-dotnettools.csharp/` | `aspire_ms_dotnettools_csharp_extension_cache` |
| ms-dotnettools.csdevkit | `ms-dotnettools.csdevkit/` | `aspire_ms_dotnettools_csdevkit_extension_cache` |
| ms-dotnettools.dotnet-interactive-vscode | `ms-dotnettools.dotnet-interactive-vscode/` | `aspire_ms_dotnettools_dotnet_interactive_vscode_extension_cache` |
| ms-azuretools.vscode-docker | `ms-azuretools.vscode-docker/` | `aspire_ms_azuretools_vscode_docker_extension_cache` |
| GitHub.copilot | `GitHub.copilot/` | `aspire_github_copilot_extension_cache` |
| GitHub.copilot-chat | `GitHub.copilot-chat/` | `aspire_github_copilot_chat_extension_cache` |
| GitHub.vscode-pull-request-github | `GitHub.vscode-pull-request-github/` | `aspire_github_vscode_pull_request_github_extension_cache` |
| eamodio.gitlens | `eamodio.gitlens/` | `aspire_eamodio_gitlens_extension_cache` |
| streetsidesoftware.code-spell-checker | `streetsidesoftware.code-spell-checker/` | `aspire_streetsidesoftware_code_spell_checker_extension_cache` |
| EditorConfig.EditorConfig | `EditorConfig.EditorConfig/` | `aspire_editorconfig_editorconfig_extension_cache` |

> **GPU Support**: The `ms-windows-ai-studio` and `GitHub.copilot` containers are configured to use the NVIDIA runtime to support local inference and tensor operations. Ensure the NVIDIA Container Toolkit is installed on the host.

## Usage

```bash
# From the repository root
docker compose -f .vscode/extensions/docker-compose.extensions.yml up -d

# Copy a cached VSIX into your host (example for GitHub Copilot)
docker compose -f .vscode/extensions/docker-compose.extensions.yml cp \
  github-copilot-extension:/opt/extensions/GitHub.copilot/GitHub.copilot.vsix \
  ./artifacts/
```

Point VS Code at the `artifacts` directory (or mount the named volume) to install the extension offline.
