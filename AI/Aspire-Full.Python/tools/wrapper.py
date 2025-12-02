#!/usr/bin/env python3
"""Thin wrapper that proxies to run.py so VS Code paths stay stable.

This wrapper ensures consistent tool invocation across:
- VS Code integrated terminal
- DevContainer environments
- CI/CD pipelines

Environment Variables (from Dockerfile):
- ASPIRE_COMPUTE_MODE: Compute mode for tensor operations
- PYTHON_GIL: GIL state (0=disabled for Python 3.15+)
"""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path
from typing import Final

# Aspire-Full.Python/tools/wrapper.py
# parents[0]=tools, parents[1]=Aspire-Full.Python, parents[2]=Root
REPO_ROOT: Final[Path] = Path(__file__).resolve().parents[2]
RUNNER: Final[Path] = REPO_ROOT / "Aspire-Full.Python" / "tools" / "lint" / "run.py"

# Environment configuration
ASPIRE_COMPUTE_MODE: Final[str] = os.environ.get("ASPIRE_COMPUTE_MODE", "gpu")


def main() -> int:
    """Main entry point."""
    command = [sys.executable, str(RUNNER), *sys.argv[1:]]
    completed = subprocess.run(command, check=False)
    return completed.returncode


if __name__ == "__main__":
    raise SystemExit(main())
