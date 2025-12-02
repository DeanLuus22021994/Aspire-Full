#!/usr/bin/env python3
"""Tensor-optimized helper for ms-dotnettools.csharp extension."""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Final

EXTENSION_ID: Final[str] = "ms-dotnettools.csharp"
IS_GPU_REQUIRED: Final[bool] = False


@dataclass(frozen=True, slots=True, kw_only=True)
class ExtensionContext:
    """Immutable extension context with cache-aligned layout."""

    extension_id: str
    cache_dir: Path
    vsix_file: Path
    extension_dir: Path
    fetcher: Path
    is_gpu_required: bool = False
    _id_hash: int = field(default=0, repr=False)

    def __post_init__(self) -> None:
        object.__setattr__(self, "_id_hash", hash(self.extension_id))

    def __hash__(self) -> int:
        return self._id_hash

    @property
    def is_cached(self) -> bool:
        return self.vsix_file.exists() and self.vsix_file.stat().st_size > 0


def get_context() -> ExtensionContext:
    """Return tensor-optimized context for C# extension."""
    extension_dir = Path(__file__).resolve().parent
    base_dir = Path(os.environ.get("EXTENSION_BASE_DIR", "/opt/extensions"))
    cache_dir = base_dir / EXTENSION_ID

    return ExtensionContext(
        extension_id=EXTENSION_ID,
        cache_dir=cache_dir,
        vsix_file=cache_dir / f"{EXTENSION_ID}.vsix",
        extension_dir=extension_dir,
        fetcher=extension_dir.parent / "fetch_extension.py",
        is_gpu_required=IS_GPU_REQUIRED,
    )
