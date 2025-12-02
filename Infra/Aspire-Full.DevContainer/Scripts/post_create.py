#!/usr/bin/env python3
"""Free-threaded friendly post-create bootstrapper for the Aspire devcontainer.

This script is the canonical implementation that lives in Infra/Aspire-Full.DevContainer/Scripts.
The .devcontainer/scripts/ folder should import or symlink to this module.
"""

from __future__ import annotations

import os
import subprocess
import sys
from collections.abc import Iterable
from pathlib import Path
from shutil import which

WORKSPACE = Path(os.environ.get("WORKSPACE_FOLDER", Path.cwd())).resolve()
SOLUTION_FILTER = WORKSPACE / "Aspire-Full.slnf"
DOTNET_TOOL_PATHS = ["/home/vscode/.dotnet/tools", "/opt/aspire/bin"]
PIPELINE_COMMAND = [
    "dotnet",
    "run",
    "--project",
    "tools/PipelineRunner/PipelineRunner.csproj",
    "--",
    "--skip-run",
]
DOTNET_TOOLS = ["dotnet-ef", "dotnet-outdated-tool"]
GH_EXTENSIONS: list[str] = [
    "github/gh-copilot",
    "github/gh-models",
    "nektos/gh-act",
    "dlvhdr/gh-dash",
    "advanced-security/gh-sbom",
    "github/gh-projects",
    "actions/gh-actions-cache",
    "githubnext/gh-aw",
    "seachicken/gh-poi",
    "chelanak/gh-changelog",
]


def _log(message: str) -> None:
    print(message, flush=True)


def _warn(message: str) -> None:
    print(message, file=sys.stderr, flush=True)


def _run(
    cmd: Iterable[str], cwd: Path | None = None, allow_failure: bool = False
) -> int:
    cmd_list = list(cmd)
    _log(f"→ {' '.join(cmd_list)}")
    completed = subprocess.run(cmd_list, cwd=cwd, check=False)
    if completed.returncode != 0 and not allow_failure:
        raise subprocess.CalledProcessError(completed.returncode, cmd_list)
    return completed.returncode


def _extend_path() -> None:
    current = os.environ.get("PATH", "")
    segments = current.split(os.pathsep) if current else []
    for entry in DOTNET_TOOL_PATHS:
        if entry not in segments:
            segments.append(entry)
    os.environ["PATH"] = os.pathsep.join(segments)


def _show_gpu_status() -> None:
    binary = which("nvidia-smi")
    if binary:
        _log("🟢 NVIDIA GPU detected inside devcontainer")
        try:
            _run(
                [
                    binary,
                    "--query-gpu=name,memory.total,memory.free,driver_version",
                    "--format=csv,noheader",
                ],
                allow_failure=True,
            )
        except subprocess.CalledProcessError:
            pass
    else:
        raise RuntimeError(
            "NVIDIA GPU utilities are not accessible in this container. "
            + "Tensor workloads strictly require CUDA."
        )


def _clone_repo_if_needed() -> None:
    if SOLUTION_FILTER.exists():
        return
    _log("📥 Cloning repository to workspace volume...")
    WORKSPACE.mkdir(parents=True, exist_ok=True)
    _run(
        ["git", "clone", "https://github.com/DeanLuus22021994/Aspire-Full.git", "."],
        cwd=WORKSPACE,
        allow_failure=True,
    )


def _run_pipeline_runner() -> None:
    if not SOLUTION_FILTER.exists():
        return
    _log("📦 Running PipelineRunner (skip run)...")
    _run(PIPELINE_COMMAND, cwd=WORKSPACE, allow_failure=True)


def _update_dotnet_tools() -> None:
    _log("🔧 Updating global .NET tools...")
    for tool in DOTNET_TOOLS:
        _run(["dotnet", "tool", "update", "-g", tool], allow_failure=True)


def _configure_git_safe_directory() -> None:
    _run(
        ["git", "config", "--global", "--add", "safe.directory", str(WORKSPACE)],
        allow_failure=True,
    )


def _install_gh_extensions() -> None:
    _log("📦 Installing GitHub CLI extensions...")
    for extension in GH_EXTENSIONS:
        _run(["gh", "extension", "install", extension], allow_failure=True)


def _install_python_package_editable() -> None:
    _log("🐍 Installing python-agents in editable mode...")
    python_agents_path = WORKSPACE / "Aspire-Full.Python" / "python-agents"
    if python_agents_path.exists():
        _run(
            ["uv", "pip", "install", "-e", ".[dev,tracing]"],
            cwd=python_agents_path,
            allow_failure=True,
        )
    else:
        _warn(f"⚠️ Could not find python-agents at {python_agents_path}")


def _run_registry_analysis() -> None:
    """Run the registry analyzer for self-enhancement automation."""
    _log("🔍 Running registry analysis for self-enhancement...")
    analyzer_path = Path(__file__).parent / "registry_analyzer.py"
    if analyzer_path.exists():
        _run(
            [sys.executable, str(analyzer_path), "--output-dir", str(WORKSPACE / "Infra" / ".config")],
            cwd=WORKSPACE,
            allow_failure=True,
        )
    else:
        _warn(f"⚠️ Registry analyzer not found at {analyzer_path}")


def main() -> None:
    """Execute the VS Code post-create automation sequence."""
    _log("🚀 Running post-create setup...")
    _extend_path()
    _show_gpu_status()
    _clone_repo_if_needed()
    _run_pipeline_runner()
    _update_dotnet_tools()
    _configure_git_safe_directory()
    _install_gh_extensions()
    _install_python_package_editable()
    _run_registry_analysis()

    _log("")
    _log("📋 Self-hosted runner info:")
    _log("   The GitHub Actions runner runs as a separate service.")
    _log("   To configure it, set GITHUB_TOKEN environment variable.")
    _log("   Use: scripts/manage-runner.ps1 -Action setup -Token <token>")
    _log("")
    _log("✅ Post-create setup complete!")


if __name__ == "__main__":
    main()
