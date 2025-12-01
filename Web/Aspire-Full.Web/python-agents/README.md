# Aspire-Full.Web · Python Agents

This folder describes the handoffs that the Aspire web frontend can make to the shared `python-agents` workspace.

## Agents

| Agent | Config | Purpose |
| --- | --- | --- |
| WebSiteHealthAgent | `site_health.agent.yaml` | Summarises release-readiness checks for the static bundle, leveraging Semantic Kernel prompts and OpenAI GPT-4.1 mini. |

## Running locally

```bash
cd python-agents
uv run python -m aspire_agents.cli run \
  --config ..\Aspire-Full.Web\python-agents\site_health.agent.yaml \
  --input "List the steps needed before publishing the Vite bundle"
```

This uses the shared Typer CLI and expects `OPENAI_API_KEY` (or Azure equivalents) to exist in your environment.

## Handoffs

`handoffs.yaml` lists identifiers that the VS Code agents and semantic pipelines can target. The identifiers are also surfaced in the YAML config so the shared runner can print them back to the user after each call.
