#!/usr/bin/env python3
"""Tensor-optimized batch extension manager.

CLI tool for managing VS Code extensions with GPU-accelerated operations.
Supports batch downloads, verification, and cache management.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import sys
from pathlib import Path
from typing import TYPE_CHECKING

# Add core module to path
_CORE_PATH = Path(__file__).resolve().parent / "core"
if str(_CORE_PATH.parent) not in sys.path:
    sys.path.insert(0, str(_CORE_PATH.parent))

from core.context import ExtensionRegistry, ExtensionState, create_context
from core.downloader import TensorDownloader
from core.hasher import GPUHasher
from core.pool import WorkerPool, run_with_pool

if TYPE_CHECKING:
    from core.context import ExtensionContext
    from core.downloader import DownloadStats

# Known extensions from docker-compose
KNOWN_EXTENSIONS = [
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


async def cmd_download(args: argparse.Namespace) -> int:
    """Download extensions command."""
    extensions = args.extensions or KNOWN_EXTENSIONS
    base_dir = Path(args.cache_dir) if args.cache_dir else None

    registry = ExtensionRegistry(capacity=len(extensions))
    contexts = [create_context(ext, base_dir) for ext in extensions]
    for ctx in contexts:
        registry.register(ctx)

    downloader = TensorDownloader(max_concurrent=args.concurrent)
    hasher = GPUHasher(use_gpu=args.gpu)

    print(f"Downloading {len(extensions)} extensions (concurrent={args.concurrent})...")

    try:
        stats_list = await downloader.download_batch(contexts)

        # Verify and report
        for ctx, stats in zip(contexts, stats_list):
            registry.update_state(
                registry._ids[ctx.extension_id],
                ExtensionState.READY,
            )
            if args.verify:
                result = hasher.hash_file(ctx.vsix_file)
                checksum = result.hex_digest[:16]
            else:
                checksum = "skipped"

            print(
                f"  ✓ {ctx.extension_id}: "
                f"{stats.bytes_downloaded / (1024 * 1024):.1f}MB "
                f"({stats.throughput_mbps:.1f}MB/s) "
                f"[{checksum}]"
            )

        print(f"\nCompleted: {registry.get_ready_count()}/{len(extensions)}")
        return 0

    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1
    finally:
        await downloader.close()


async def cmd_verify(args: argparse.Namespace) -> int:
    """Verify cached extensions command."""
    extensions = args.extensions or KNOWN_EXTENSIONS
    base_dir = Path(args.cache_dir) if args.cache_dir else None

    hasher = GPUHasher(use_gpu=args.gpu)
    contexts = [create_context(ext, base_dir) for ext in extensions]

    print(f"Verifying {len(extensions)} extensions (GPU={hasher.is_gpu_enabled})...")

    valid = 0
    for ctx in contexts:
        if not ctx.vsix_file.exists():
            print(f"  ✗ {ctx.extension_id}: not found")
            continue

        result = hasher.hash_file(ctx.vsix_file)
        size_mb = result.file_size / (1024 * 1024)
        print(
            f"  ✓ {ctx.extension_id}: "
            f"{size_mb:.1f}MB "
            f"[{result.hex_digest[:16]}] "
            f"(GPU={result.gpu_accelerated})"
        )
        valid += 1

    print(f"\nValid: {valid}/{len(extensions)}")
    return 0 if valid == len(extensions) else 1


async def cmd_status(args: argparse.Namespace) -> int:
    """Show extension cache status."""
    extensions = args.extensions or KNOWN_EXTENSIONS
    base_dir = Path(args.cache_dir) if args.cache_dir else None

    contexts = [create_context(ext, base_dir) for ext in extensions]

    status = {
        "extensions": [],
        "summary": {"total": len(extensions), "cached": 0, "missing": 0},
    }

    for ctx in contexts:
        exists = ctx.vsix_file.exists()
        size = ctx.vsix_file.stat().st_size if exists else 0

        ext_status = {
            "id": ctx.extension_id,
            "cached": exists,
            "size_bytes": size,
            "path": str(ctx.vsix_file),
            "gpu_required": ctx.is_gpu_required,
        }
        status["extensions"].append(ext_status)

        if exists:
            status["summary"]["cached"] += 1
        else:
            status["summary"]["missing"] += 1

    if args.json:
        print(json.dumps(status, indent=2))
    else:
        print(f"Extension Cache Status ({base_dir or '/opt/extensions'}):\n")
        for ext in status["extensions"]:
            icon = "✓" if ext["cached"] else "✗"
            size = f"{ext['size_bytes'] / (1024 * 1024):.1f}MB" if ext["cached"] else "missing"
            gpu = " [GPU]" if ext["gpu_required"] else ""
            print(f"  {icon} {ext['id']}: {size}{gpu}")

        print(
            f"\nSummary: {status['summary']['cached']}/{status['summary']['total']} cached"
        )

    return 0


def main() -> int:
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Tensor-optimized VS Code extension manager",
        formatter_class=argparse.RawDescriptionHelpFormatter,
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
