#!/usr/bin/env python3
"""Helper metadata for the GitHub.copilot-chat extension."""

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
    cache_dir = Path("/opt/extensions") / "GitHub.copilot-chat"
    return ExtensionContext(
        extension_id="GitHub.copilot-chat",
        cache_dir=cache_dir,
        vsix_file=cache_dir / "GitHub.copilot-chat.vsix",
    )
