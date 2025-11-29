#!/usr/bin/env python3
"""Thin wrapper that proxies to run.py so VS Code paths stay stable."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

# Aspire-Full.Python/tools/wrapper.py
# parents[0]=tools, parents[1]=Aspire-Full.Python, parents[2]=Root
REPO_ROOT = Path(__file__).resolve().parents[2]
RUNNER = REPO_ROOT / "Aspire-Full.Python" / "tools" / "lint" / "run.py"


def main() -> int:
    command = [sys.executable, str(RUNNER), *sys.argv[1:]]
    completed = subprocess.run(command, check=False)
    return completed.returncode


if __name__ == "__main__":
    raise SystemExit(main())
