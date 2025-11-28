#!/bin/bash
set -euo pipefail

# Legacy shim that forwards to the Python implementation so existing tooling keeps working.
python3 /workspace/.devcontainer/scripts/post_start.py "$@"
