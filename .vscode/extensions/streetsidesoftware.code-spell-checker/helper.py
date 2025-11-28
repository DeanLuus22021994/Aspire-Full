#!/usr/bin/env python3
"""Helper metadata for the streetsidesoftware.code-spell-checker extension."""

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
    """Return metadata for the Code Spell Checker extension cache."""
    extension_dir = Path(__file__).resolve().parent
    cache_dir = Path("/opt/extensions") / "streetsidesoftware.code-spell-checker"
    return ExtensionContext(
        extension_id="streetsidesoftware.code-spell-checker",
        cache_dir=cache_dir,
        vsix_file=cache_dir / "streetsidesoftware.code-spell-checker.vsix",
        extension_dir=extension_dir,
        fetcher=extension_dir.parent / "fetch_extension.py",
    )
