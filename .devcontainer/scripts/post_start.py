#!/usr/bin/env python3
"""Python-based post-start hook that favors free-threaded CPython runtimes."""

from __future__ import annotations

import os
import shutil
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Iterable

WORKSPACE = Path("/workspace")
DASHBOARD_HEALTH_URL = "http://aspire-dashboard:18888/health"


def _log(message: str) -> None:
    print(message, flush=True)


def _capture(cmd: Iterable[str]) -> str:
    try:
        completed = subprocess.run(
            list(cmd),
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        output = completed.stdout.strip()
        return output.splitlines()[0] if output else "not available"
    except (FileNotFoundError, subprocess.CalledProcessError):
        return "not available"


def _extend_path() -> None:
    extras = ["/home/vscode/.dotnet/tools", "/opt/aspire/bin"]
    current = os.environ.get("PATH", "")
    segments = current.split(os.pathsep) if current else []
    for entry in extras:
        if entry not in segments:
            segments.append(entry)
    os.environ["PATH"] = os.pathsep.join(segments)


def _wait_for_dashboard() -> None:
    _log("🔍 Checking Aspire Dashboard connectivity...")
    for attempt in range(1, 11):
        try:
            with urllib.request.urlopen(DASHBOARD_HEALTH_URL, timeout=2):
                _log("✅ Aspire Dashboard is ready at http://localhost:18888")
                return
        except urllib.error.URLError:
            _log(f"⏳ Waiting for Aspire Dashboard... (attempt {attempt}/10)")
            time.sleep(2)
    _log("⚠️ Aspire Dashboard did not report healthy within the allotted attempts.")


def _gpu_summary() -> str:
    nvidia_smi = shutil.which("nvidia-smi")
    if not nvidia_smi:
        return "not detected (tensor workloads will run on CPU)"
    try:
        completed = subprocess.run(
            [
                nvidia_smi,
                "--query-gpu=name,driver_version",
                "--format=csv,noheader",
            ],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        return completed.stdout.strip().splitlines()[0]
    except subprocess.CalledProcessError:
        return "detected but query failed"


def main() -> None:
    """Run the VS Code post-start diagnostics and readiness logging."""
    _extend_path()
    _log("🚀 Running post-start setup...")
    _wait_for_dashboard()

    _log("")
    _log("📋 Environment Info:")
    _log(f"   .NET SDK: {_capture(['dotnet', '--version'])}")
    _log(f"   Aspire CLI: {_capture(['aspire', '--version'])}")
    _log(f"   Docker: {_capture(['docker', '--version'])}")
    _log(f"   gh CLI: {_capture(['gh', '--version'])}")
    _log(f"   GPU: {_gpu_summary()}")
    _log("")
    _log("🌐 Services:")
    _log("   Aspire Dashboard:  http://localhost:18888")
    _log("   OTLP Endpoint:     http://aspire-dashboard:18889")
    _log("   MCP Server:        http://localhost:16036")
    _log("   PostgreSQL:        localhost:5432")
    _log("   Redis:             localhost:6379")
    _log("")
    _log("🤖 GitHub Copilot Integration:")
    _log("   1. Launch your Aspire app from VS Code (F5)")
    _log("   2. Open Aspire Dashboard: http://localhost:18888")
    _log("   3. Click the Copilot button in the top-right corner")
    _log("   4. MCP config is in .vscode/mcp.json")
    _log("")
    _log("✅ Development environment ready!")


if __name__ == "__main__":
    main()
