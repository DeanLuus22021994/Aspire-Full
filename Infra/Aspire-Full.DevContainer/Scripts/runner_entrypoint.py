#!/usr/bin/env python3
"""GitHub Actions self-hosted runner entrypoint for the Aspire devcontainer.

This script is the canonical implementation that lives in Infra/Aspire-Full.DevContainer/Scripts.
The .devcontainer/scripts/ folder should import or symlink to this module.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

RUNNER_HOME = Path("/home/runner")
RUNNER_WORK = RUNNER_HOME / "_work"
ACTIONS_RUNNER_URL = "https://github.com/actions/runner/releases/download/v2.321.0/actions-runner-linux-x64-2.321.0.tar.gz"


def _log(message: str) -> None:
    print(message, flush=True)


def _run(cmd: list[str], cwd: Path | None = None, check: bool = True) -> int:
    _log(f"→ {' '.join(cmd)}")
    result = subprocess.run(cmd, cwd=cwd, check=check)
    return result.returncode


def _ensure_runner_installed() -> None:
    """Download and extract the runner if not already present."""
    config_script = RUNNER_HOME / "config.sh"
    if config_script.exists():
        _log("✅ Runner already installed")
        return

    _log("📥 Downloading GitHub Actions runner...")
    RUNNER_HOME.mkdir(parents=True, exist_ok=True)
    tarball = RUNNER_HOME / "runner.tar.gz"

    _run(["curl", "-L", "-o", str(tarball), ACTIONS_RUNNER_URL])
    _run(["tar", "-xzf", str(tarball), "-C", str(RUNNER_HOME)])
    tarball.unlink(missing_ok=True)
    _log("✅ Runner extracted")


def _configure_runner(token: str, url: str, name: str, labels: str) -> None:
    """Configure the runner with the provided token."""
    config_script = RUNNER_HOME / "config.sh"
    if not config_script.exists():
        raise RuntimeError("Runner not installed. Call _ensure_runner_installed first.")

    _log(f"🔧 Configuring runner '{name}' for {url}...")
    cmd = [
        str(config_script),
        "--url",
        url,
        "--token",
        token,
        "--name",
        name,
        "--labels",
        labels,
        "--work",
        str(RUNNER_WORK),
        "--unattended",
        "--replace",
    ]
    _run(cmd, cwd=RUNNER_HOME)


def _start_runner() -> None:
    """Start the runner in the foreground."""
    run_script = RUNNER_HOME / "run.sh"
    if not run_script.exists():
        raise RuntimeError("Runner not configured. Call _configure_runner first.")

    _log("🚀 Starting GitHub Actions runner...")
    os.execv(str(run_script), [str(run_script)])


def main() -> None:
    """Entry point for the runner container."""
    token = os.environ.get("GITHUB_TOKEN")
    url = os.environ.get(
        "GITHUB_REPOSITORY_URL", "https://github.com/DeanLuus22021994/Aspire-Full"
    )
    name = os.environ.get("RUNNER_NAME", "aspire-devcontainer")
    labels = os.environ.get("RUNNER_LABELS", "self-hosted,linux,x64,gpu,aspire")

    if not token:
        _log("❌ GITHUB_TOKEN environment variable is required")
        _log("   Set it via: docker run -e GITHUB_TOKEN=<token> ...")
        sys.exit(1)

    _ensure_runner_installed()
    _configure_runner(token, url, name, labels)
    _start_runner()


if __name__ == "__main__":
    main()
