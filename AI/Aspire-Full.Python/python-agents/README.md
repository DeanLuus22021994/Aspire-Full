# Aspire Python Agents

This workspace hosts the shared Semantic Kernel (SK) scaffolding for folder-scoped agents that can be invoked as VS Code handoffs or background tasks. The goals for this package are:

- provide a single `uv` project targeting the free-threaded CPython 3.15 runtime (`cpython-3.15t`)
- keep common tooling (SK kernel wiring, tracing, Typer CLI, lint/type/test commands) in one place
- let each Aspire folder drop a lightweight `python-agents/` descriptor that references the shared CLI plus its own prompts, skills, and handoff metadata

## Quick start

1. Install the requested interpreter (pyenv or uv):

   ```bash
   uv python install 3.15t
   uv venv .venv --python 3.15t
   uv sync
   ```

2. Export the model credentials that match your provider. OpenAI by default:

   ```bash
   $env:OPENAI_API_KEY = "sk-..."
   ```

3. Verify CUDA/Tensor Core access (agents refuse to start without it and will pin `cuda:0` with Tensor Core math optimizations):

   ```bash
   uv run python - <<'PY'
   from aspire_agents.gpu import ensure_tensor_core_gpu
   print(ensure_tensor_core_gpu())
   PY
   ```

4. Run an agent config (for example the Web status agent):

   ```bash
   uv run python -m aspire_agents.cli run \
     --config ..\Aspire-Full.Web\python-agents\site_health.agent.yaml \
     --input "List pre-flight checks before shipping a new static bundle"
   ```

## Project layout

```
python-agents/
├─ pyproject.toml             # hatchling + uv-managed dependencies
├─ README.md
├─ src/aspire_agents/
│  ├─ cli.py                  # Typer entry point
│  ├─ config.py               # YAML/ENV loaders for agent manifests
│  ├─ kernel.py               # Semantic Kernel builder helpers
│  ├─ gpu.py                  # Tensor Core enforcement + metadata helpers
│  └─ runner.py               # Async agent runner with telemetry hooks
└─ scripts/run_agent.ps1      # Convenience wrapper for Windows handoffs
```

Additional folders (e.g., `src/aspire_agents/agents/web`) can be added as shared skills/utilities once more folders opt in.

## Tooling

- **Run**: `uv run python -m aspire_agents.cli run --config <path> --input "..."`
- **Lint**: `uv run ruff check && uv run ruff format --check`
- **Type-check**: `uv run mypy src`
- **Tests**: `uv run pytest`

All commands assume you are inside `python-agents/` with the uv-managed virtual environment active. The repo keeps formatting/type/test hooks lightweight so folder-specific agents can contribute tests alongside their manifests.
