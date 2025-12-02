#!/usr/bin/env python3
"""Tensor-optimized handler for ms-windows-ai-studio extension.

Features:
- Async I/O with zero-copy streaming
- GPU-accelerated SHA-256 verification (critical for AI Studio)
- Memory-mapped file writes
- Free-threading support (PYTHON_GIL=0)
"""

from __future__ import annotations

import asyncio
import os
import sys
from pathlib import Path
from typing import TYPE_CHECKING, Final

# Add parent to path for shared modules
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from fetch_extension import TensorDownloader, GPUHasher, _HAS_AIOHTTP
from helper import get_context

if TYPE_CHECKING:
    from helper import ExtensionContext

# GPU-required extension - AI Studio needs GPU acceleration
IS_GPU_EXTENSION: Final[bool] = True


async def download_async(context: ExtensionContext) -> int:
    """Tensor-optimized async download with GPU hash verification."""
    downloader = TensorDownloader(max_concurrent=1)
    hasher = GPUHasher(use_gpu=IS_GPU_EXTENSION)

    try:
        print(f"Downloading {context.extension_id} (GPU={hasher.is_gpu_enabled})...")
        stats = await downloader.download(
            context.extension_id,
            context.cache_dir,
            verify=True,
        )

        print(f"✓ {context.extension_id}:")
        print(f"  Size: {stats.bytes_downloaded / (1024 * 1024):.2f} MB")
        print(f"  Throughput: {stats.throughput_mbps:.2f} MB/s")
        print(f"  SHA256: {stats.checksum.hex()[:32]}...")
        return 0

    except Exception as e:
        print(f"✗ {context.extension_id}: {e}", file=sys.stderr)
        return 1
    finally:
        await downloader.close()


def main(argv: list[str] | None = None) -> None:
    """Download the Windows AI Studio VSIX into the cache."""
    _ = argv or sys.argv[1:]
    context = get_context()

    # Set free-threading mode
    os.environ.setdefault("PYTHON_GIL", "0")

    if _HAS_AIOHTTP:
        exit_code = asyncio.run(download_async(context))
        sys.exit(exit_code)
    else:
        # Legacy subprocess fallback
        import subprocess
        env = os.environ.copy()
        env["EXTENSION_ID"] = context.extension_id
        env["EXTENSION_CACHE"] = str(context.cache_dir)
        subprocess.run(
            [sys.executable, str(context.fetcher)],
            check=True,
            env=env,
            cwd=context.extension_dir,
        )


if __name__ == "__main__":
    main()
