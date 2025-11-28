#!/usr/bin/env python3
"""Thin wrapper to run pytest through tools/python/test/run.py."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
RUNNER = REPO_ROOT / "tools" / "python" / "test" / "run.py"


def main() -> int:
    command = [sys.executable, str(RUNNER), *sys.argv[1:]]
    completed = subprocess.run(command, check=False)
    return completed.returncode


if __name__ == "__main__":
    raise SystemExit(main())
