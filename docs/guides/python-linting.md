# Python Linting

Python lint settings are centralized to keep VS Code, VS Code Insiders, and CLI tooling aligned.

- Source of truth: `.config/python-lint.yaml` (YAML/JSON). Update this file to change the global line length or shared rules.
- Generator: `python tools/python-lint/sync_configs.py` reads the YAML and rewrites `.flake8` and `.pylintrc` with the derived settings.
- Editors: VS Code and VS Code Insiders automatically pick up `.flake8` and `.pylintrc`, so no custom settings.json overrides are required.

After editing the YAML file, run the sync script (or re-run the task defined above) to propagate updates. This keeps both linters consistent while still using their native config formats for compatibility.
