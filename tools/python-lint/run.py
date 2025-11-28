#!/usr/bin/env python3
"""Entry point that lints only first-party Python directories."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path
from typing import Iterable, List

REPO_ROOT = Path(__file__).resolve().parents[2]
CONFIG_DIR = REPO_ROOT / ".config"
ROOTS_FILE = CONFIG_DIR / "python-lint-roots.txt"
EXCLUDES_FILE = CONFIG_DIR / "python-lint-excludes.txt"


def _read_lines(path: Path) -> List[str]:
    if not path.exists():
        return []
    return [
        line.strip()
        for line in path.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]


def _collect_targets() -> List[str]:
    targets: List[str] = []
    for relative in _read_lines(ROOTS_FILE):
        candidate = REPO_ROOT / relative
        if candidate.exists():
            targets.append(str(candidate))
    return targets


def _build_command(extra_args: Iterable[str], targets: Iterable[str]) -> List[str]:
    command = [sys.executable, "-m", "pylint"]
    command.extend(extra_args)
    command.extend(targets)
    return command


def main(argv: list[str] | None = None) -> int:
    args = argv if argv is not None else sys.argv[1:]
    targets = _collect_targets()
    if not targets:
        message = (
            "No lint targets were found. Did you run "
            "tools/python-lint/sync_configs.py?"
        )
        print(message, file=sys.stderr)
        return 0
    excludes = _read_lines(EXCLUDES_FILE)
    if excludes:
        print(f"Skipping excluded entries: {', '.join(excludes)}")
    command = _build_command(args, targets)
    print("Running:", " ".join(command))
    completed = subprocess.run(command, cwd=REPO_ROOT, check=False)
    return completed.returncode


if __name__ == "__main__":
    raise SystemExit(main())
