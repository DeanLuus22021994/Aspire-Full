#!/usr/bin/env python3
"""Helper metadata for the ms-dotnettools.csharp extension."""

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
    cache_dir = Path("/opt/extensions") / "ms-dotnettools.csharp"
    return ExtensionContext(
        extension_id="ms-dotnettools.csharp",
        cache_dir=cache_dir,
        vsix_file=cache_dir / "ms-dotnettools.csharp.vsix",
    )
