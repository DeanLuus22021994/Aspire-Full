#!/usr/bin/env python3
"""Helper metadata for the ms-dotnettools.csharp extension."""

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
    extension_dir = Path(__file__).resolve().parent
    cache_dir = Path("/opt/extensions") / "ms-dotnettools.csharp"
    return ExtensionContext(
        extension_id="ms-dotnettools.csharp",
        cache_dir=cache_dir,
        vsix_file=cache_dir / "ms-dotnettools.csharp.vsix",
        extension_dir=extension_dir,
        fetcher=extension_dir.parent / "fetch_extension.py",
    )
