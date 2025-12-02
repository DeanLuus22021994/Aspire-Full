#!/usr/bin/env python3
"""Tensor-optimized batch extension manager.

CLI tool for managing VS Code extensions with GPU-accelerated operations.
Supports batch downloads, verification, and cache management.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Final, TypedDict

# Import from standalone fetch_extension (no core dependency)
from fetch_extension import (
    TensorDownloader,
    GPUHasher,
    DownloadStats,
    _HAS_AIOHTTP,
    _HAS_CUPY,
)

# Known extensions from docker-compose
KNOWN_EXTENSIONS: Final[list[str]] = [
    "GitHub.copilot",
    "GitHub.copilot-chat",
    "GitHub.vscode-pull-request-github",
    "ms-windows-ai-studio.windows-ai-studio",
    "ms-azuretools.vscode-azure-github-copilot",
    "ms-azuretools.vscode-docker",
    "ms-dotnettools.csharp",
    "ms-dotnettools.csdevkit",
    "ms-dotnettools.dotnet-interactive-vscode",
    "eamodio.gitlens",
    "streetsidesoftware.code-spell-checker",
    "EditorConfig.EditorConfig",
]

# GPU-required extensions
GPU_EXTENSIONS: Final[frozenset[str]] = frozenset({
    "GitHub.copilot",
    "ms-windows-ai-studio.windows-ai-studio",
})


class ExtensionStatus(TypedDict):
    """Type for extension status dict."""

    id: str
    cached: bool
    size_bytes: int
    path: str
    gpu_required: bool


class StatusSummary(TypedDict):
    """Type for status summary dict."""

    total: int
    cached: int
    missing: int


class StatusReport(TypedDict):
    """Type for full status report."""

    extensions: list[ExtensionStatus]
    summary: StatusSummary


@dataclass(frozen=True, slots=True)
class ExtensionInfo:
    """Lightweight extension metadata."""

    extension_id: str
    cache_dir: Path
    vsix_file: Path
    is_gpu_required: bool

    @property
    def is_cached(self) -> bool:
        return self.vsix_file.exists() and self.vsix_file.stat().st_size > 0


def get_extension_info(extension_id: str, base_dir: Path | None = None) -> ExtensionInfo:
    """Create extension info from ID."""
    base = base_dir or Path(os.environ.get("EXTENSION_BASE_DIR", "/opt/extensions"))
    cache_dir = base / extension_id
    return ExtensionInfo(
        extension_id=extension_id,
        cache_dir=cache_dir,
        vsix_file=cache_dir / f"{extension_id}.vsix",
        is_gpu_required=extension_id in GPU_EXTENSIONS,
    )


async def cmd_download(args: argparse.Namespace) -> int:
    """Download extensions command."""
    if not _HAS_AIOHTTP:
        print("Error: aiohttp required for async downloads", file=sys.stderr)
        print("Install with: pip install aiohttp", file=sys.stderr)
        return 1

    extensions = args.extensions or KNOWN_EXTENSIONS
    base_dir = Path(args.cache_dir) if args.cache_dir else None
    infos = [get_extension_info(ext, base_dir) for ext in extensions]

    downloader = TensorDownloader(max_concurrent=args.concurrent)
    hasher = GPUHasher(use_gpu=args.gpu and _HAS_CUPY)

    print(f"Downloading {len(extensions)} extensions (concurrent={args.concurrent})...")
    print(f"GPU hashing: {hasher.is_gpu_enabled}")

    success = 0
    failed = 0

    try:
        for info in infos:
            try:
                stats = await downloader.download(
                    info.extension_id,
                    info.cache_dir,
                    verify=args.verify,
                )

                if args.verify:
                    digest, _ = hasher.hash_file(info.vsix_file)
                    checksum = digest.hex()[:16]
                else:
                    checksum = "skipped"

                size_mb = stats.bytes_downloaded / (1024 * 1024)
                throughput = stats.throughput_mbps
                msg = f"  ✓ {info.extension_id}: {size_mb:.1f}MB ({throughput:.1f}MB/s) [{checksum}]"
                print(msg)
                success += 1

            except Exception as e:
                print(f"  ✗ {info.extension_id}: {e}", file=sys.stderr)
                failed += 1

        print(f"\nCompleted: {success}/{len(extensions)} (failed: {failed})")
        return 0 if failed == 0 else 1

    finally:
        await downloader.close()


async def cmd_verify(args: argparse.Namespace) -> int:
    """Verify cached extensions command."""
    extensions = args.extensions or KNOWN_EXTENSIONS
    base_dir = Path(args.cache_dir) if args.cache_dir else None
    infos = [get_extension_info(ext, base_dir) for ext in extensions]

    hasher = GPUHasher(use_gpu=args.gpu and _HAS_CUPY)
    print(f"Verifying {len(extensions)} extensions (GPU={hasher.is_gpu_enabled})...")

    valid = 0
    for info in infos:
        if not info.vsix_file.exists():
            print(f"  ✗ {info.extension_id}: not found")
            continue

        digest, blocks = hasher.hash_file(info.vsix_file)
        size_mb = info.vsix_file.stat().st_size / (1024 * 1024)
        hash_prefix = digest.hex()[:16]
        msg = f"  ✓ {info.extension_id}: {size_mb:.1f}MB [{hash_prefix}] ({blocks} blocks)"
        print(msg)
        valid += 1

    print(f"\nValid: {valid}/{len(extensions)}")
    return 0 if valid == len(extensions) else 1


async def cmd_status(args: argparse.Namespace) -> int:
    """Show extension cache status."""
    extensions = args.extensions or KNOWN_EXTENSIONS
    base_dir = Path(args.cache_dir) if args.cache_dir else None
    infos = [get_extension_info(ext, base_dir) for ext in extensions]

    status: StatusReport = {
        "extensions": [],
        "summary": {"total": len(extensions), "cached": 0, "missing": 0},
    }

    for info in infos:
        exists = info.vsix_file.exists()
        size = info.vsix_file.stat().st_size if exists else 0

        ext_status: ExtensionStatus = {
            "id": info.extension_id,
            "cached": exists,
            "size_bytes": size,
            "path": str(info.vsix_file),
            "gpu_required": info.is_gpu_required,
        }
        status["extensions"].append(ext_status)

        if exists:
            status["summary"]["cached"] += 1
        else:
            status["summary"]["missing"] += 1

    if args.json:
        print(json.dumps(status, indent=2))
    else:
        display_dir = base_dir or Path("/opt/extensions")
        print(f"Extension Cache Status ({display_dir}):\n")
        for ext in status["extensions"]:
            icon = "✓" if ext["cached"] else "✗"
            if ext["cached"]:
                size = f"{ext['size_bytes'] / (1024 * 1024):.1f}MB"
            else:
                size = "missing"
            gpu = " [GPU]" if ext["gpu_required"] else ""
            print(f"  {icon} {ext['id']}: {size}{gpu}")

        cached = status["summary"]["cached"]
        total = status["summary"]["total"]
        print(f"\nSummary: {cached}/{total} cached")

    return 0


def main() -> int:
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Tensor-optimized VS Code extension manager",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s download                    # Download all known extensions
  %(prog)s download GitHub.copilot     # Download specific extension
  %(prog)s verify                      # Verify all cached extensions
  %(prog)s status --json               # Show status as JSON
        """,
    )
    parser.add_argument(
        "--cache-dir",
        type=str,
        default=None,
        help="Base directory for extension cache (default: /opt/extensions)",
    )
    parser.add_argument(
        "--gpu",
        action="store_true",
        default=True,
        help="Use GPU acceleration when available",
    )
    parser.add_argument(
        "--no-gpu",
        action="store_false",
        dest="gpu",
        help="Disable GPU acceleration",
    )

    subparsers = parser.add_subparsers(dest="command", help="Commands")

    # Download command
    dl_parser = subparsers.add_parser("download", help="Download extensions")
    dl_parser.add_argument(
        "extensions",
        nargs="*",
        help="Extension IDs (default: all known)",
    )
    dl_parser.add_argument(
        "-c", "--concurrent",
        type=int,
        default=4,
        help="Maximum concurrent downloads",
    )
    dl_parser.add_argument(
        "--verify",
        action="store_true",
        default=True,
        help="Verify downloads with hash",
    )
    dl_parser.add_argument(
        "--no-verify",
        action="store_false",
        dest="verify",
        help="Skip hash verification",
    )

    # Verify command
    verify_parser = subparsers.add_parser("verify", help="Verify cached extensions")
    verify_parser.add_argument(
        "extensions",
        nargs="*",
        help="Extension IDs (default: all known)",
    )

    # Status command
    status_parser = subparsers.add_parser("status", help="Show cache status")
    status_parser.add_argument(
        "extensions",
        nargs="*",
        help="Extension IDs (default: all known)",
    )
    status_parser.add_argument(
        "--json",
        action="store_true",
        help="Output as JSON",
    )

    args = parser.parse_args()

    if args.command is None:
        parser.print_help()
        return 0

    # Run async command
    if args.command == "download":
        return asyncio.run(cmd_download(args))
    elif args.command == "verify":
        return asyncio.run(cmd_verify(args))
    elif args.command == "status":
        return asyncio.run(cmd_status(args))

    return 0


if __name__ == "__main__":
    sys.exit(main())
