# GitHub MCP Container

Builds the `tools/github-mcp` server into a production Node image with Tini, GPU-ready base, and writable cache locations. When paired with the docker-compose file in this directory it exposes port `17071` and persists:

- `/opt/github-mcp/data`
- `/var/log/github-mcp`
- `/home/node/.cache/github-mcp`

Configure the container via environment variables such as `GITHUB_MCP_TOKEN`, `GITHUB_MCP_REPOSITORY`, and `GITHUB_MCP_API_KEY`.
