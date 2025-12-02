#!/usr/bin/env python3
"""Tensor-optimized handler for ms-dotnettools.csharp extension."""

from __future__ import annotations

import sys
from pathlib import Path

# Add parent to path for shared modules
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from base_handler import create_handler

# Extension metadata
EXTENSION_ID = "ms-dotnettools.csharp"
IS_GPU_REQUIRED = False  # C# extension doesn't need GPU


def main(argv: list[str] | None = None) -> None:
    """Download the C# VSIX into the cache."""
    handler = create_handler(EXTENSION_ID, is_gpu_required=IS_GPU_REQUIRED)
    handler.run(argv)


if __name__ == "__main__":
    main()
