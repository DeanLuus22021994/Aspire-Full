#!/usr/bin/env python3
"""Helper metadata for the ms-azuretools.vscode-azure-github-copilot extension."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True, slots=True)
class ExtensionContext:
    """Describe the extension cache layout and runtime paths."""

    extension_id: str
    cache_dir: Path
    vsix_file: Path
    extension_dir: Path
    fetcher: Path


def get_context() -> ExtensionContext:
    """Return metadata for the Azure GitHub Copilot extension cache."""
    extension_dir = Path(__file__).resolve().parent
    cache_dir = Path("/opt/extensions") / "ms-azuretools.vscode-azure-github-copilot"
    return ExtensionContext(
        extension_id="ms-azuretools.vscode-azure-github-copilot",
        cache_dir=cache_dir,
        vsix_file=cache_dir / "ms-azuretools.vscode-azure-github-copilot.vsix",
        extension_dir=extension_dir,
        fetcher=extension_dir.parent / "fetch_extension.py",
    )
