#!/usr/bin/env python3
"""Extension management core - delegates to centralized AI automation.

GPU workers are now in AI/Aspire-Full.Python/python-agents/src/aspire_agents/automation.py
- 2 x 1GB VRAM workers
- 1GB VRAM Qdrant (same subnet)
- GPU-only, no CPU fallback
"""

from __future__ import annotations

from .context import ExtensionContext, ExtensionRegistry
from .downloader import TensorDownloader
from .hasher import GPUHasher

# Configuration: 2 workers @ 1GB VRAM each
HOT_GPU_WORKERS = 2
WORKER_VRAM_MB = 1024
QDRANT_VRAM_MB = 1024

__all__ = [
    # Context
    "ExtensionContext",
    "ExtensionRegistry",
    # Download
    "TensorDownloader",
    # Hashing
    "GPUHasher",
    # Configuration
    "HOT_GPU_WORKERS",
    "WORKER_VRAM_MB",
    "QDRANT_VRAM_MB",
]
