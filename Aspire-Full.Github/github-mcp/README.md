# Aspire GitHub MCP

This package hosts a Model Context Protocol (MCP) server that feeds GitHub repository
telemetry plus Aspire dashboard health into the GitHub Copilot extensions. It is designed
for containerized scenarios and boots with warm caches so Copilot always receives
useful context even before the rest of the stack finishes starting.

## Features

- Repo health summaries (issues, PRs, releases, stars, watchers).
- Workflow run snapshots for spotting failing CI before debugging Aspire.
- Aspire dashboard health + metrics rollup so Copilot can explain runtime issues while you work from the host.
- Named volume persistence for npm cache, log history, and warm telemetry data.
- HTTP Streamable MCP transport with optional API-key auth compatible with Copilot Chat / CLI.
- GPU-friendly container (NVIDIA runtime + Tensor Core access) so Copilot context stays available even when Aspire workloads occupy the GPU.

## Configuration

Environment variables (all mapped inside Docker Compose):

| Variable | Required | Description |
| --- | --- | --- |
| `GITHUB_MCP_TOKEN` | ✅ | Fine-grained PAT or GitHub App token with `repo` + `actions:read`. |
| `GITHUB_MCP_REPOSITORY` | ✅ | Repository in `owner/name` format (defaults to workspace repo). |
| `GITHUB_MCP_API_KEY` | ➖ | Optional key the VS Code MCP profile must send via `x-mcp-api-key`. |
| `ASPIRE_DASHBOARD_URL` | ➖ | Defaults to `http://aspire-dashboard:18888`. |
| `ASPIRE_DASHBOARD_MCP_ENDPOINT_URL` | ➖ | When Aspire exposes its own MCP instance (forwarded to Copilot). |
| `GITHUB_MCP_CACHE_SECONDS` | ➖ | Cache TTL to reduce GitHub API calls (default 30s). |
| `GITHUB_MCP_PORT` | ➖ | Host port (default `17071`). |

## Running locally

```bash
cd tools/github-mcp
npm install
npm run build
GITHUB_MCP_TOKEN=ghp_123 GITHUB_MCP_REPOSITORY=DeanLuus22021994/Aspire-Full npm start
```

Then configure `.vscode/mcp.json` so Copilot knows about `http://localhost:17071/mcp` and
forwards the same API key that the container expects.

### Local demo / experimentation

If you need to tinker with the streamable HTTP example that ships with the MCP SDK, use the
checked-in copy instead of editing files under `node_modules`:

```bash
npm run demo:simple-stream -- --oauth # pass --oauth/--oauth-strict flags as needed
```

The implementation lives in `src/examples` so any tweaks remain under source control and survive
`npm install` / dependency updates.

### MCP support layer & ownership guard

All MCP SDK touch-points live under `src/mcp-support`. Re-exporting the SDK from this
layer keeps us in control of transport/auth primitives and guarantees the repo still
functions when the upstream example changes. ESLint now enforces this policy with a
`no-restricted-imports` rule, so if you need a new type or helper, add it to the
support barrel first and import from there everywhere else.

## Docker support

The root `.devcontainer/docker-compose.yml` defines a `github-mcp` service based on
`node:22-slim` that bakes all dependencies up-front, requests NVIDIA GPUs, and uses the
following named volumes:

- `aspire-github-mcp-cache` – npm cache + warm data so installs stay instant.
- `aspire-github-mcp-data` – persisted telemetry cache and session state.
- `aspire-github-mcp-logs` – structured history accessible from the host while debugging.

The image exposes `/healthz` for readiness probes and `/metrics/summary` (via the Aspire
Dashboard) so Copilot can attribute dashboard failures to specific services.
