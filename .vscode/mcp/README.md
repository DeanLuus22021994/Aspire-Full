# MCP Server Containers

Local Docker build contexts and a compose file for the MCP servers declared in `.vscode/mcp.json`. Running these services keeps both the Aspire Dashboard MCP endpoint and the GitHub MCP server warm with zero startup latency.

## Services

| Server | Folder | Ports | Persistent volumes |
| --- | --- | --- | --- |
| Aspire Dashboard | `aspire-dashboard/` | 18888, 18889, 16036 | `aspire_mcp_dashboard_data` |
| GitHub MCP | `github-mcp/` | 17071 | `aspire_mcp_github_data`, `aspire_mcp_github_logs`, `aspire_mcp_github_cache` |

## Usage

```bash
# From the repo root
cd .vscode/mcp

docker compose -f docker-compose.mcp.yml up -d
```

Use the VS Code MCP configuration in `.vscode/mcp.json` (already pointing to `http://localhost:16036` and `http://localhost:17071/mcp`) to talk to the running containers.
