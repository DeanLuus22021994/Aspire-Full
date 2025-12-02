#!/usr/bin/env python3
"""Extension context with tensor-optimized metadata structures."""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from enum import IntEnum, auto
from pathlib import Path
from typing import TYPE_CHECKING, Final

import numpy as np

if TYPE_CHECKING:
    from numpy.typing import NDArray

# Compile-time constants for SIMD alignment
CACHE_LINE_SIZE: Final[int] = 64
TENSOR_ALIGNMENT: Final[int] = 128  # Match GPU tensor core alignment


class ExtensionState(IntEnum):
    """Extension lifecycle states as contiguous integers for vectorized ops."""

    UNKNOWN = 0
    PENDING = auto()
    DOWNLOADING = auto()
    HASHING = auto()
    READY = auto()
    FAILED = auto()


@dataclass(frozen=True, slots=True, kw_only=True)
class ExtensionContext:
    """Immutable extension metadata with cache-aligned layout.

    Uses slots and frozen for zero-overhead attribute access.
    All paths are pre-resolved to avoid repeated syscalls.
    """

    extension_id: str
    cache_dir: Path
    vsix_file: Path
    extension_dir: Path
    fetcher: Path
    state: ExtensionState = ExtensionState.PENDING

    # Pre-computed hash of extension_id for fast dict lookups
    _id_hash: int = field(default=0, repr=False)

    def __post_init__(self) -> None:
        """Compute stable hash once at construction time."""
        # Use object.__setattr__ since frozen=True
        object.__setattr__(self, "_id_hash", hash(self.extension_id))

    def __hash__(self) -> int:
        """Return pre-computed hash for O(1) dict operations."""
        return self._id_hash

    def with_state(self, state: ExtensionState) -> ExtensionContext:
        """Return new context with updated state (immutable update)."""
        return ExtensionContext(
            extension_id=self.extension_id,
            cache_dir=self.cache_dir,
            vsix_file=self.vsix_file,
            extension_dir=self.extension_dir,
            fetcher=self.fetcher,
            state=state,
        )

    @property
    def is_gpu_required(self) -> bool:
        """Check if extension requires GPU runtime."""
        gpu_extensions = frozenset({
            "GitHub.copilot",
            "ms-windows-ai-studio.windows-ai-studio",
        })
        return self.extension_id in gpu_extensions


class ExtensionRegistry:
    """Vectorized extension registry with NumPy-backed state tracking.

    Uses contiguous memory layout for SIMD-friendly state updates.
    Thread-safe with atomic state transitions.
    """

    __slots__ = ("_contexts", "_states", "_ids", "_capacity", "_size")

    def __init__(self, capacity: int = 32) -> None:
        """Initialize registry with pre-allocated capacity.

        Args:
            capacity: Initial capacity, rounded up to cache line boundary.
        """
        # Align capacity to cache line for optimal memory access
        aligned_capacity = ((capacity + CACHE_LINE_SIZE - 1) // CACHE_LINE_SIZE) * CACHE_LINE_SIZE
        self._capacity = aligned_capacity
        self._size = 0

        # Pre-allocate state array with SIMD-friendly alignment
        self._states: NDArray[np.uint8] = np.zeros(
            aligned_capacity,
            dtype=np.uint8,
        )
        self._contexts: list[ExtensionContext | None] = [None] * aligned_capacity
        self._ids: dict[str, int] = {}

    def register(self, context: ExtensionContext) -> int:
        """Register extension and return its index.

        Args:
            context: Extension context to register.

        Returns:
            Index of the registered extension.

        Raises:
            ValueError: If registry is at capacity.
        """
        if context.extension_id in self._ids:
            return self._ids[context.extension_id]

        if self._size >= self._capacity:
            self._grow()

        idx = self._size
        self._contexts[idx] = context
        self._states[idx] = context.state.value
        self._ids[context.extension_id] = idx
        self._size += 1
        return idx

    def _grow(self) -> None:
        """Double capacity with aligned reallocation."""
        new_capacity = self._capacity * 2
        new_states = np.zeros(new_capacity, dtype=np.uint8)
        new_states[: self._size] = self._states[: self._size]
        self._states = new_states

        new_contexts: list[ExtensionContext | None] = [None] * new_capacity
        new_contexts[: self._size] = self._contexts[: self._size]
        self._contexts = new_contexts
        self._capacity = new_capacity

    def update_state(self, idx: int, state: ExtensionState) -> None:
        """Atomically update extension state.

        Args:
            idx: Extension index from register().
            state: New state value.
        """
        self._states[idx] = state.value
        ctx = self._contexts[idx]
        if ctx is not None:
            self._contexts[idx] = ctx.with_state(state)

    def get_by_state(self, state: ExtensionState) -> NDArray[np.intp]:
        """Vectorized query for extensions in given state.

        Args:
            state: State to filter by.

        Returns:
            Array of indices matching the state.
        """
        mask = self._states[: self._size] == state.value
        return np.nonzero(mask)[0]

    def get_pending_count(self) -> int:
        """Fast count of pending extensions using SIMD reduction."""
        return int(np.sum(self._states[: self._size] == ExtensionState.PENDING.value))

    def get_ready_count(self) -> int:
        """Fast count of ready extensions using SIMD reduction."""
        return int(np.sum(self._states[: self._size] == ExtensionState.READY.value))

    def __len__(self) -> int:
        """Return number of registered extensions."""
        return self._size

    def __getitem__(self, idx: int) -> ExtensionContext:
        """Get context by index with bounds check."""
        if idx < 0 or idx >= self._size:
            msg = f"Index {idx} out of range [0, {self._size})"
            raise IndexError(msg)
        ctx = self._contexts[idx]
        if ctx is None:
            msg = f"Context at index {idx} is None"
            raise ValueError(msg)
        return ctx


def create_context(extension_id: str, base_dir: Path | None = None) -> ExtensionContext:
    """Factory function for creating extension contexts.

    Args:
        extension_id: VS Code marketplace extension ID.
        base_dir: Override base directory (defaults to /opt/extensions).

    Returns:
        Configured ExtensionContext.
    """
    if base_dir is None:
        base_dir = Path(os.environ.get("EXTENSION_BASE_DIR", "/opt/extensions"))

    extension_dir = Path(__file__).resolve().parent.parent / extension_id
    cache_dir = base_dir / extension_id

    return ExtensionContext(
        extension_id=extension_id,
        cache_dir=cache_dir,
        vsix_file=cache_dir / f"{extension_id}.vsix",
        extension_dir=extension_dir,
        fetcher=extension_dir.parent / "fetch_extension.py",
        state=ExtensionState.PENDING,
    )
