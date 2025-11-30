#!/usr/bin/env python3
"""Unified pytest runner backed by the shared YAML config."""

from __future__ import annotations

import importlib.util
import subprocess
import sys
from pathlib import Path
from types import ModuleType
from typing import Any, Iterable, Tuple

REPO_ROOT = Path(__file__).resolve().parents[3]
CONFIG_MODULE_PATH = Path(__file__).resolve().parents[1] / "config.py"


def _load_config_module() -> ModuleType:
    """Load the config module."""
    spec = importlib.util.spec_from_file_location("tools_config", CONFIG_MODULE_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load config module from {CONFIG_MODULE_PATH}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


CONFIG_MODULE = _load_config_module()
CONFIG_REPO_ROOT = getattr(CONFIG_MODULE, "REPO_ROOT")
TestConfigType = Any
load_test_config = getattr(CONFIG_MODULE, "load_test_config")
collect_existing_test_roots = getattr(CONFIG_MODULE, "collect_existing_test_roots")
is_vendor_path = getattr(CONFIG_MODULE, "is_vendor_path")

if CONFIG_REPO_ROOT != REPO_ROOT:
    raise RuntimeError("Mismatch between tool repo root and .config python module.")


def _partition_args(args: Iterable[str]) -> Tuple[list[str], list[str], bool]:
    """Partition arguments into options and targets."""
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
    targets: Iterable[str], config: TestConfigType
) -> tuple[list[str], list[str]]:
    """Filter out vendor targets."""
    kept: list[str] = []
    dropped: list[str] = []
    for target in targets:
        if is_vendor_path(target, config.vendor_globs):
            dropped.append(target)
        else:
            kept.append(target)
    return kept, dropped


def _build_command(
    config: TestConfigType,
    options: list[str],
    targets: list[str],
    append_double_dash: bool,
) -> list[str]:
    """Build the pytest command."""
    command = [
        sys.executable,
        "-m",
        "pytest",
        *list(config.runner.default_addopts or ()),
    ]
    if options:
        command.extend(options)
    if targets:
        if append_double_dash:
            command.append("--")
        command.extend(targets)
    return command


def main(argv: list[str] | None = None) -> int:
    """Main entry point."""
    config = load_test_config()
    args = argv if argv is not None else sys.argv[1:]
    options, targets, saw_double_dash = _partition_args(args)
    if targets:
        filtered, dropped = _filter_vendor_targets(targets, config)
        if dropped and not filtered:
            print("All pytest targets filtered; skipping vendor bundle.")
            return 0
        if dropped:
            print(f"Skipping vendor targets: {', '.join(dropped)}")
        command = _build_command(config, options, filtered, saw_double_dash)
        return subprocess.run(command, cwd=REPO_ROOT, check=False).returncode

    auto_targets = collect_existing_test_roots(config)
    if not auto_targets:
        print("No pytest targets were found in test_roots from python-config.yaml.")
        return 0
    command = _build_command(config, options, auto_targets, False)
    print("Running:", " ".join(command))
    return subprocess.run(command, cwd=REPO_ROOT, check=False).returncode


if __name__ == "__main__":
    raise SystemExit(main())
