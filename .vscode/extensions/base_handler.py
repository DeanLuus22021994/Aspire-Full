#!/usr/bin/env python3
"""Shared base handler for tensor-optimized extension downloads.

This module provides a factory pattern for creating extension handlers
with consistent tensor-optimized behavior across all extensions.
"""

from __future__ import annotations

import asyncio
import os
import sys
from pathlib import Path
from typing import TYPE_CHECKING, Callable, Final

# Add parent to path for shared modules
_PARENT = Path(__file__).resolve().parent
if str(_PARENT) not in sys.path:
    sys.path.insert(0, str(_PARENT))

from fetch_extension import TensorDownloader, GPUHasher, _HAS_AIOHTTP

if TYPE_CHECKING:
    from dataclasses import dataclass
    from fetch_extension import DownloadStats


class BaseHandler:
    """Base handler with tensor-optimized download capabilities.

    Subclasses only need to provide extension-specific metadata.
    All tensor optimizations (async I/O, GPU hashing, mmap) are inherited.
    """

    __slots__ = ("_extension_id", "_is_gpu_required", "_cache_dir", "_extension_dir")

    def __init__(
        self,
        extension_id: str,
        extension_dir: Path,
        is_gpu_required: bool = False,
        cache_base: Path | None = None,
    ) -> None:
        """Initialize handler with extension metadata.

        Args:
            extension_id: VS Code marketplace extension ID.
            extension_dir: Path to extension directory.
            is_gpu_required: Whether extension benefits from GPU acceleration.
            cache_base: Base cache directory (default: /opt/extensions).
        """
        self._extension_id = extension_id
        self._is_gpu_required = is_gpu_required
        self._extension_dir = extension_dir

        base = cache_base or Path(os.environ.get("EXTENSION_BASE_DIR", "/opt/extensions"))
        self._cache_dir = base / extension_id

    @property
    def extension_id(self) -> str:
        """Extension marketplace ID."""
        return self._extension_id

    @property
    def cache_dir(self) -> Path:
        """Cache directory for this extension."""
        return self._cache_dir

    @property
    def vsix_file(self) -> Path:
        """Path to cached VSIX file."""
        return self._cache_dir / f"{self._extension_id}.vsix"

    @property
    def is_cached(self) -> bool:
        """Check if extension is already downloaded."""
        return self.vsix_file.exists() and self.vsix_file.stat().st_size > 0

    async def download_async(self) -> int:
        """Tensor-optimized async download with GPU hash verification.

        Returns:
            Exit code (0 for success, non-zero for failure).
        """
        downloader = TensorDownloader(max_concurrent=1)
        hasher = GPUHasher(use_gpu=self._is_gpu_required)

        try:
            print(f"Downloading {self._extension_id} (GPU={hasher.is_gpu_enabled})...")
            stats = await downloader.download(
                self._extension_id,
                self._cache_dir,
                verify=True,
            )

            print(f"✓ {self._extension_id}:")
            print(f"  Size: {stats.bytes_downloaded / (1024 * 1024):.2f} MB")
            print(f"  Throughput: {stats.throughput_mbps:.2f} MB/s")
            print(f"  SHA256: {stats.checksum.hex()[:32]}...")
            return 0

        except Exception as e:
            print(f"✗ {self._extension_id}: {e}", file=sys.stderr)
            return 1
        finally:
            await downloader.close()

    def run(self, argv: list[str] | None = None) -> None:
        """Execute download with automatic fallback.

        Args:
            argv: Command line arguments (unused, for interface compatibility).
        """
        _ = argv or sys.argv[1:]

        # Set free-threading mode
        os.environ.setdefault("PYTHON_GIL", "0")

        if _HAS_AIOHTTP:
            exit_code = asyncio.run(self.download_async())
            sys.exit(exit_code)
        else:
            # Legacy subprocess fallback
            import subprocess
            env = os.environ.copy()
            env["EXTENSION_ID"] = self._extension_id
            env["EXTENSION_CACHE"] = str(self._cache_dir)
            fetcher = self._extension_dir.parent / "fetch_extension.py"
            subprocess.run(
                [sys.executable, str(fetcher)],
                check=True,
                env=env,
                cwd=self._extension_dir,
            )


def create_handler(
    extension_id: str,
    is_gpu_required: bool = False,
) -> BaseHandler:
    """Factory function to create a handler for an extension.

    Args:
        extension_id: VS Code marketplace extension ID.
        is_gpu_required: Whether extension benefits from GPU acceleration.

    Returns:
        Configured BaseHandler instance.
    """
    # Determine extension directory from caller's frame
    import inspect
    caller_frame = inspect.stack()[1]
    extension_dir = Path(caller_frame.filename).resolve().parent

    return BaseHandler(
        extension_id=extension_id,
        extension_dir=extension_dir,
        is_gpu_required=is_gpu_required,
    )


# GPU-required extensions list
GPU_EXTENSIONS: Final[frozenset[str]] = frozenset({
    "GitHub.copilot",
    "ms-windows-ai-studio.windows-ai-studio",
})


def is_gpu_extension(extension_id: str) -> bool:
    """Check if extension requires GPU acceleration."""
    return extension_id in GPU_EXTENSIONS
