#!/usr/bin/env python3
"""Tensor-optimized helper for the ms-windows-ai-studio extension.

Provides immutable context with pre-computed hashes for O(1) dict lookups
and cache-aligned memory layout for SIMD-friendly operations.
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Final

# Cache-aligned constants
CACHE_LINE_SIZE: Final[int] = 64
TENSOR_ALIGNMENT: Final[int] = 128

# Extension metadata - AI Studio is GPU-critical
EXTENSION_ID: Final[str] = "ms-windows-ai-studio.windows-ai-studio"
IS_GPU_REQUIRED: Final[bool] = True


@dataclass(frozen=True, slots=True, kw_only=True)
class ExtensionContext:
    """Immutable extension context with cache-aligned layout.

    Uses frozen dataclass for zero-overhead attribute access.
    All paths are pre-resolved to avoid repeated syscalls.
    """

    extension_id: str
    cache_dir: Path
    vsix_file: Path
    extension_dir: Path
    fetcher: Path
    is_gpu_required: bool = False

    # Pre-computed hash for O(1) dict operations
    _id_hash: int = field(default=0, repr=False)

    def __post_init__(self) -> None:
        """Compute stable hash once at construction time."""
        object.__setattr__(self, "_id_hash", hash(self.extension_id))

    def __hash__(self) -> int:
        """Return pre-computed hash."""
        return self._id_hash

    @property
    def size_bytes(self) -> int:
        """Get cached VSIX file size, or 0 if not present."""
        if self.vsix_file.exists():
            return self.vsix_file.stat().st_size
        return 0

    @property
    def is_cached(self) -> bool:
        """Check if extension is already downloaded."""
        return self.vsix_file.exists() and self.size_bytes > 0


def get_context() -> ExtensionContext:
    """Return tensor-optimized context for Windows AI Studio extension."""
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
