#!/usr/bin/env python3
"""Extension management core - delegates to centralized AI automation.

GPU workers are now in AI/Aspire-Full.Python/python-agents/src/aspire_agents/automation.py
- Python 3.15t free-threaded (64-bit, PYTHON_GIL=0)
- 2 x 1GB VRAM workers, tightly coupled with CUDA stream sync
- 1GB VRAM Qdrant (same subnet)
- CuPy hyper-virtual GPU compute with type-safe arrays
- Pre-embeddings for zero-latency dispatch
- NO CPU FALLBACK - GPU-only
"""

from __future__ import annotations

from .context import ExtensionContext, ExtensionRegistry
from .downloader import TensorDownloader
from .hasher import GPUHasher

# Configuration: 2 workers @ 1GB VRAM each (Python 3.15t)
HOT_GPU_WORKERS = 2
WORKER_VRAM_MB = 1024
QDRANT_VRAM_MB = 1024
EMBEDDING_DIM = 384
MAX_EMBEDDINGS = 1024
TARGET_LATENCY_NS = 0  # Zero latency

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
    "EMBEDDING_DIM",
    "MAX_EMBEDDINGS",
    "TARGET_LATENCY_NS",
]
