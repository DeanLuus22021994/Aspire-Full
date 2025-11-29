#!/usr/bin/env python3
"""Handler for managing the Aspire Dashboard MCP container with docker compose."""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

from helper import get_context

VALID_ACTIONS = {"up", "down", "logs"}


def _compose_base(compose_file: Path) -> list[str]:
    return ["docker", "compose", "-f", str(compose_file)]


def _build_command(action: str, service_name: str, compose_file: Path) -> list[str]:
    base = _compose_base(compose_file)
    if action == "up":
        return base + ["up", "-d", service_name]
    if action == "down":
        return base + ["stop", service_name]
    if action == "logs":
        return base + ["logs", "-f", service_name]
    raise ValueError(f"Unsupported action '{action}'")


def main(argv: list[str] | None = None) -> None:
    """Control the Aspire Dashboard MCP service via docker compose."""
    args = argv or sys.argv[1:]
    action = args[0] if args else "up"
    extra_args = args[1:]
    if action not in VALID_ACTIONS:
        raise SystemExit(f"Action must be one of {sorted(VALID_ACTIONS)}")

    context = get_context()
    command = _build_command(action, context.service_name, context.compose_file)
    command.extend(extra_args)

    env = os.environ.copy()
    env.setdefault("PYTHON_GIL", "0")
    env["MCP_SERVICE"] = context.service_name

    # Ensure the data volume directory exists if mapped locally (optional, but good practice)
    # Docker compose usually handles named volumes, but if bind mounts are used:
    # data_dir = context.project_root / "data"
    # data_dir.mkdir(exist_ok=True)

    print(f"Executing: {' '.join(command)}")
    subprocess.run(  # noqa: S603
        command,
        check=True,
        env=env,
        cwd=context.project_root,
    )


if __name__ == "__main__":
    main()
