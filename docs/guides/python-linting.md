# Python Linting

Python lint settings are centralized to keep VS Code, VS Code Insiders, and CLI tooling aligned.

- Source of truth: `.config/python-lint.yaml` (YAML/JSON). Update this file to change the global line length or shared rules.
- Generator: `python tools/python-lint/sync_configs.py` reads the YAML and rewrites `.flake8` and `.pylintrc` with the derived settings.
- Runner: `python tools/python-lint/run.py` shells out to `pylint` but only targets the directories listed in `.config/python-lint-roots.txt`, so vendor bundles are never linted even if an editor misbehaves. Pass additional CLI args after `--` as needed.
- Editors: VS Code and VS Code Insiders automatically pick up `.flake8` and `.pylintrc`. To prevent bundled extensions under `.vscode/` and `.vscode-test/` from triggering diagnostics, the workspace sets:
	```jsonc
	"python.linting.ignorePatterns": [
		".vscode",
		".vscode/**",
		".vscode-test",
		".vscode-test/**",
		".vscode-*",
		".vscode-*/**"
	],
	"python.analysis.exclude": [
		".vscode",
		".vscode/**",
		".vscode-test",
		".vscode-test/**",
		".vscode-*",
		".vscode-*/**"
	]
	```
	Keep these arrays in sync with `.config/python-lint.yaml` whenever new vendor directories need to be muted.

After editing the YAML file, run the sync script (or re-run the task defined above) to propagate updates. This keeps both linters consistent while still using their native config formats for compatibility.
