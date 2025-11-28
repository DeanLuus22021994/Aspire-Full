# ms-windows-ai-studio.windows-ai-studio Cache

This container downloads the `ms-windows-ai-studio.windows-ai-studio` VS Code extension into a persistent volume so the VSIX is always available offline.

- **Cache path**: `/opt/extensions/ms-windows-ai-studio.windows-ai-studio`
- **Named volume**: `aspire_ms_windows_ai_studio_windows_ai_studio_extension_cache`
- **Service**: `ms-windows-ai-studio-extension`

Start it with `docker compose -f .vscode/extensions/docker-compose.extensions.yml up -d ms-windows-ai-studio-extension` or run the `extensions:start` VS Code task.
