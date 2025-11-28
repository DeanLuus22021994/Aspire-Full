#!/usr/bin/env python3
"""Helper metadata for the GitHub.vscode-pull-request-github extension."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True, slots=True)
class ExtensionContext:
    """Describe the extension cache layout."""

    extension_id: str
    cache_dir: Path
    vsix_file: Path


def get_context() -> ExtensionContext:
    cache_dir = Path("/opt/extensions") / "GitHub.vscode-pull-request-github"
    return ExtensionContext(
        extension_id="GitHub.vscode-pull-request-github",
        cache_dir=cache_dir,
        vsix_file=cache_dir / "GitHub.vscode-pull-request-github.vsix",
    )
