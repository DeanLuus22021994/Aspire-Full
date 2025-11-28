#!/usr/bin/env python3
"""Python replacement for the GitHub Actions runner entrypoint (free-threaded ready)."""

from __future__ import annotations

import json
import os
import signal
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

RUNNER_ROOT = Path("/home/runner/actions-runner")
RUNNER_CONFIG_FLAG = RUNNER_ROOT / ".runner"
DEFAULT_RUNNER_NAME = f"aspire-runner-{os.environ.get('HOSTNAME', 'container')}"
RUNNER_NAME = os.environ.get("RUNNER_NAME", DEFAULT_RUNNER_NAME)
RUNNER_LABELS = os.environ.get(
    "RUNNER_LABELS", "self-hosted,Linux,X64,docker,dotnet,aspire"
)
RUNNER_GROUP = os.environ.get("RUNNER_GROUP", "Default")
RUNNER_WORKDIR = os.environ.get("RUNNER_WORKDIR", "/home/runner/_work")
GITHUB_TOKEN = os.environ.get("GITHUB_TOKEN")
GITHUB_REPOSITORY = os.environ.get("GITHUB_REPOSITORY")
MAX_DOCKER_ATTEMPTS = 30
_runner_process: subprocess.Popen[str] | None = None


class RunnerError(RuntimeError):
    """Raised when the runner cannot be configured."""


def _log(level: str, message: str) -> None:
    palette = {
        "INFO": "\033[0;32m",
        "WARN": "\033[1;33m",
        "ERROR": "\033[0;31m",
    }
    color = palette.get(level, "")
    suffix = "\033[0m" if color else ""
    print(f"{color}[{level}] {message}{suffix}", flush=True)


def _require_env(value: str | None, name: str) -> str:
    if not value:
        raise RunnerError(f"Environment variable {name} is required")
    return value


def _run(cmd: list[str], allow_failure: bool = False) -> int:
    _log("INFO", f"→ {' '.join(cmd)}")
    completed = subprocess.run(cmd, cwd=RUNNER_ROOT, check=False, text=True)
    if completed.returncode != 0 and not allow_failure:
        raise subprocess.CalledProcessError(completed.returncode, cmd)
    return completed.returncode


def _wait_for_docker() -> None:
    _log("INFO", "Waiting for Docker daemon...")
    for attempt in range(1, MAX_DOCKER_ATTEMPTS + 1):
        if subprocess.run(["docker", "info"], check=False).returncode == 0:
            _log("INFO", "Docker daemon is ready")
            return
        _log("WARN", f"Docker not ready, attempt {attempt}/{MAX_DOCKER_ATTEMPTS}...")
        time.sleep(2)
    raise RunnerError("Docker daemon did not become ready in time")


def _github_post(endpoint: str) -> dict[str, Any]:
    token = _require_env(GITHUB_TOKEN, "GITHUB_TOKEN")
    repo = _require_env(GITHUB_REPOSITORY, "GITHUB_REPOSITORY")
    url = f"https://api.github.com/repos/{repo}/{endpoint}"
    request = urllib.request.Request(url, method="POST")
    request.add_header("Authorization", f"token {token}")
    request.add_header("Accept", "application/vnd.github+json")
    request.add_header("X-GitHub-Api-Version", "2022-11-28")
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            payload = response.read().decode("utf-8")
            return json.loads(payload)
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8")
        raise RunnerError(f"GitHub API request failed: {exc.code} {body}") from exc


def _get_registration_token() -> str:
    data = _github_post("actions/runners/registration-token")
    token = data.get("token")
    if not token:
        raise RunnerError("Failed to obtain runner registration token")
    return token


def _configure_runner() -> None:
    if RUNNER_CONFIG_FLAG.exists():
        _log("INFO", "Runner already configured, skipping configuration")
        return

    token = _get_registration_token()
    _log("INFO", f"Configuring runner: {RUNNER_NAME}")
    _log("INFO", f"Labels: {RUNNER_LABELS}")
    _log("INFO", f"Work directory: {RUNNER_WORKDIR}")

    _run(
        [
            "./config.sh",
            "--url",
            f"https://github.com/{_require_env(GITHUB_REPOSITORY, 'GITHUB_REPOSITORY')}",
            "--token",
            token,
            "--name",
            RUNNER_NAME,
            "--labels",
            RUNNER_LABELS,
            "--runnergroup",
            RUNNER_GROUP,
            "--work",
            RUNNER_WORKDIR,
            "--unattended",
            "--replace",
        ]
    )


def _remove_runner() -> None:
    if not RUNNER_CONFIG_FLAG.exists():
        return
    try:
        data = _github_post("actions/runners/remove-token")
    except RunnerError:
        return
    token = data.get("token")
    if not token:
        return
    _run(["./config.sh", "remove", "--token", token], allow_failure=True)


def _launch_runner() -> None:
    global _runner_process
    _log("INFO", "Starting runner...")
    _runner_process = subprocess.Popen(["./run.sh"], cwd=RUNNER_ROOT)
    _runner_process.wait()


def _handle_signal(signum: int, _frame: Any) -> None:
    _log("WARN", f"Received signal {signum}, shutting down runner...")
    if _runner_process and _runner_process.poll() is None:
        _runner_process.terminate()
    _remove_runner()
    sys.exit(0)


def main() -> None:
    _log("INFO", "==========================================")
    _log("INFO", "GitHub Actions Self-Hosted Runner")
    _log("INFO", "==========================================")
    _log(
        "INFO",
        f"Runner Version: {(RUNNER_ROOT / 'bin/runner.version').read_text().strip() if (RUNNER_ROOT / 'bin/runner.version').exists() else 'unknown'}",
    )
    _log("INFO", f".NET SDK: {_capture_cli(['dotnet', '--version'])}")
    _log("INFO", f"Node.js: {_capture_cli(['node', '--version'])}")
    _log("INFO", f"npm: {_capture_cli(['npm', '--version'])}")
    _log("INFO", "==========================================")

    _wait_for_docker()
    _log("INFO", f"Docker: {_capture_cli(['docker', '--version'])}")

    _configure_runner()

    signal.signal(signal.SIGTERM, _handle_signal)
    signal.signal(signal.SIGINT, _handle_signal)
    signal.signal(signal.SIGQUIT, _handle_signal)

    _launch_runner()


def _capture_cli(cmd: list[str]) -> str:
    try:
        result = subprocess.run(
            cmd, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True
        )
        return result.stdout.strip() or "not available"
    except (subprocess.CalledProcessError, FileNotFoundError):
        return "not available"


if __name__ == "__main__":
    try:
        main()
    except RunnerError as exc:
        _log("ERROR", str(exc))
        sys.exit(1)
