# Python Testing

Pytest shares the same YAML source of truth as the lint pipeline so editors, CLI scripts, and CI jobs all agree on defaults.

- **Config**: `.config/config.yaml` → `contexts.python.test`. Update `paths.test_roots`, `vendor_globs`, or `pytest.addopts` there; everything else is generated.
- **Generator**: `python tools/python/test/sync_configs.py` consumes the YAML and rewrites `pytest.ini`, `.config/python/test/roots.yaml`, `.config/python/test/excludes.yaml`, plus the VS Code friendly wrapper at `.config/python/test/wrapper.py`.
- **Runner**: `python tools/python/test/run.py` shells out to `pytest` with the shared defaults. When the CLI is invoked without explicit targets it auto-discovers directories from `runner.auto_targets`, filters anything under `vendor_globs`, and (new behavior) silently skips directories that do not contain pytest-style files so `.NET`-only folders such as `sandboxes/` no longer trigger the "no tests ran" error.
- **Editors**: Point VS Code (`python.testing.pytestPath`) or any task runner to `.config/python/test/wrapper.py` so single-file executions and UI buttons hit the same filtering logic as the CLI.

## Adding tests

1. Place Python tests under one of the configured roots (currently `python-agents/tests`, `sandboxes`, or `scripts/tests`).
2. Follow pytest naming (`test_*.py` or `*_test.py`). Only directories containing such files are passed to `pytest`.
3. Run `python tools/python/test/run.py` or `python tools/python/test/sync_configs.py` after configuration changes to regenerate artifacts for editors/CI.

Example:

```bash
cd python-agents
uv run pytest
# or from the repo root
python tools/python/test/run.py
```

With this flow, both CLI and editor-triggered runs share the same settings, vendor filtering, and discovery heuristics.
