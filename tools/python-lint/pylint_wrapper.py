#!/usr/bin/env python3
"""VS Code entry point that filters vendor files before running pylint."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path
from typing import Iterable, List

REPO_ROOT = Path(__file__).resolve().parents[2]
BLOCKED_SEGMENTS = (
    "/.vscode/",
    "/.vscode-test/",
    "/.vscode-",
)


def _normalize(path: str) -> str:
    return path.replace("\\", "/").lower()


def _is_blocked_target(arg: str) -> bool:
    normalized = _normalize(arg)
    return any(segment in normalized for segment in BLOCKED_SEGMENTS)


def _filter_args(args: Iterable[str]) -> tuple[List[str], List[str]]:
    passthrough: List[str] = []
    dropped: List[str] = []
    for arg in args:
        if arg == "--":
            passthrough.append(arg)
            continue
        if arg.startswith("-"):
            passthrough.append(arg)
            continue
        if _is_blocked_target(arg):
            dropped.append(arg)
            continue
        passthrough.append(arg)
    return passthrough, dropped


def _run_pylint(args: List[str]) -> int:
    command = [sys.executable, "-m", "pylint", *args]
    completed = subprocess.run(command, cwd=REPO_ROOT, check=False)
    return completed.returncode


def main() -> int:
    raw_args = sys.argv[1:]
    filtered_args, dropped = _filter_args(raw_args)
    if dropped and not any(
        not token.startswith("-") and token != "--" for token in filtered_args
    ):
        print("All pylint targets filtered; skipping vendor bundle.")
        return 0
    return _run_pylint(filtered_args)


if __name__ == "__main__":
    raise SystemExit(main())
