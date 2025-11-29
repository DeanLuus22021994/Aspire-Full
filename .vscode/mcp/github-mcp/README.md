# GitHub MCP Container

Builds the `tools/github-mcp` server into a production Node image with Tini, GPU-ready base, and writable cache locations. 

**Enhanced Features:**
- **NVIDIA Runtime Support**: Uses `nvidia/cuda:12.4.1-runtime-ubuntu22.04` to leverage GPU acceleration for embedding operations.
- **Persistence**: Explicitly defines volumes for data, logs, and cache to ensure state preservation across restarts.

When paired with the docker-compose file in this directory it exposes port `17071` and persists:

- `/opt/github-mcp/data`
- `/var/log/github-mcp`
- `/home/node/.cache/github-mcp`

Configure the container via environment variables such as `GITHUB_MCP_TOKEN`, `GITHUB_MCP_REPOSITORY`, and `GITHUB_MCP_API_KEY`.
