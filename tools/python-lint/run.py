#!/usr/bin/env python3
"""Unified Pylint runner for both CLI usage and VS Code integration."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path
from typing import Iterable, Tuple

THIS_DIR = Path(__file__).resolve().parent
if str(THIS_DIR) not in sys.path:
    sys.path.insert(0, str(THIS_DIR))

from lint_config import (  # type: ignore  # noqa: E402
    REPO_ROOT,
    LintConfig,
    collect_existing_roots,
    is_vendor_path,
    load_config,
)


def _partition_args(args: Iterable[str]) -> Tuple[list[str], list[str], bool]:
    options: list[str] = []
    targets: list[str] = []
    after_double_dash = False
    for arg in args:
        if after_double_dash:
            targets.append(arg)
            continue
        if arg == "--":
            after_double_dash = True
            continue
        if arg.startswith("-") and arg != "-":
            options.append(arg)
        else:
            targets.append(arg)
    return options, targets, after_double_dash


def _filter_vendor_targets(
    targets: Iterable[str], config: LintConfig
) -> tuple[list[str], list[str]]:
    kept: list[str] = []
    dropped: list[str] = []
    for target in targets:
        if is_vendor_path(target, config.vendor_globs):
            dropped.append(target)
        else:
            kept.append(target)
    return kept, dropped


def _invoke(command: list[str]) -> int:
    completed = subprocess.run(command, cwd=REPO_ROOT, check=False)
    return completed.returncode


def _build_command(
    config: LintConfig,
    options: list[str],
    targets: list[str],
    append_double_dash: bool,
) -> list[str]:
    command = [sys.executable, "-m", "pylint", *options]
    if targets:
        if append_double_dash:
            command.append("--")
        command.extend(targets)
    return command


def main(argv: list[str] | None = None) -> int:
    config = load_config()
    args = argv if argv is not None else sys.argv[1:]
    options, targets, saw_double_dash = _partition_args(args)
    if targets:
        filtered, dropped = _filter_vendor_targets(targets, config)
        if dropped and not filtered:
            print("All pylint targets filtered; skipping vendor bundle.")
            return 0
        if dropped:
            print(f"Skipping vendor targets: {', '.join(dropped)}")
        command = _build_command(config, options, filtered, saw_double_dash)
        return _invoke(command)

    auto_targets = collect_existing_roots(config)
    if not auto_targets:
        print(
            "No lint targets were found in lint_roots from python-lint.yaml.",
            file=sys.stderr,
        )
        return 0
    command = _build_command(config, options, auto_targets, False)
    print("Running:", " ".join(command))
    return _invoke(command)


if __name__ == "__main__":
    raise SystemExit(main())
