#!/usr/bin/env python3
"""Tensor-optimized extension management core.

Provides 3 hot GPU workers for low-latency high-throughput automation.
"""

from __future__ import annotations

from .context import ExtensionContext, ExtensionRegistry
from .downloader import TensorDownloader
from .hasher import GPUHasher
from .pool import (
    GPUWorkerStats,
    HotGPUWorkerPool,
    HotGPUWorkerState,
    TaskPriority,
    WorkerPool,
    run_with_hot_gpu_pool,
    run_with_pool,
)

# Hot GPU worker count
HOT_GPU_WORKERS = 3

__all__ = [
    # Context
    "ExtensionContext",
    "ExtensionRegistry",
    # Download
    "TensorDownloader",
    # Hashing
    "GPUHasher",
    # Worker Pool
    "WorkerPool",
    "TaskPriority",
    # Hot GPU Workers
    "HOT_GPU_WORKERS",
    "HotGPUWorkerPool",
    "HotGPUWorkerState",
    "GPUWorkerStats",
    "run_with_pool",
    "run_with_hot_gpu_pool",
]
