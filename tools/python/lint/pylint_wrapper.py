#!/usr/bin/env python3
"""Thin wrapper that proxies to run.py so VS Code paths stay stable."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

RUNNER = Path(__file__).with_name("run.py")


def main() -> int:
    command = [sys.executable, str(RUNNER), *sys.argv[1:]]
    completed = subprocess.run(command, check=False)
    return completed.returncode


if __name__ == "__main__":
    raise SystemExit(main())
