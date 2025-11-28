# ms-azuretools.vscode-azure-github-copilot Cache

Caches the `ms-azuretools.vscode-azure-github-copilot` extension VSIX inside a dedicated volume.

- **Cache path**: `/opt/extensions/ms-azuretools.vscode-azure-github-copilot`
- **Named volume**: `aspire_ms_azuretools_vscode_azure_github_copilot_extension_cache`
- **Service**: `ms-azuretools-vscode-azure-github-copilot-extension`

Launch via `docker compose -f .vscode/extensions/docker-compose.extensions.yml up -d ms-azuretools-vscode-azure-github-copilot-extension` or run the `extensions:start` task.
