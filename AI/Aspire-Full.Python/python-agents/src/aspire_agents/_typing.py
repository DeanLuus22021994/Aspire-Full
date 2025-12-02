"""Type definitions and runtime utilities for Python 3.16+ free-threading.

This module provides:
- Type-safe wrappers for PyTorch CUDA functions with incomplete stubs
- Free-threading detection via public Python 3.13+ APIs
- Explicit type definitions for CUDA device properties

Thread Safety:
- All functions are thread-safe for GIL-free execution
- Uses public Python APIs (no private _ prefixed calls)
"""

from __future__ import annotations

import sys
from dataclasses import dataclass
from typing import Final

# ============================================================================
# Free-Threading Detection (Python 3.13+ public API)
# ============================================================================


def is_gil_disabled() -> bool:
    """Check if Python GIL is disabled using public API.

    Python 3.13+ provides `sys.flags.free_threading` as the public API
    for detecting free-threaded builds. The private `sys._is_gil_enabled()`
    should not be used in type-checked code.

    Returns:
        True if running in free-threaded mode (PYTHON_GIL=0)

    Examples:
        >>> if is_gil_disabled():
        ...     print("Running in free-threaded mode")
    """
    # Python 3.13+ public API via sys.flags
    flags = getattr(sys, "flags", None)
    if flags is not None and hasattr(flags, "free_threading"):
        return bool(flags.free_threading)
    return False


def get_gil_status_string() -> str:
    """Get human-readable GIL status string.

    Returns:
        'disabled' if free-threaded, 'enabled' otherwise
    """
    return "disabled" if is_gil_disabled() else "enabled"


# ============================================================================
# PyTorch CUDA Type Definitions
# ============================================================================


@dataclass(frozen=True, slots=True)
class CudaDeviceProperties:
    """Type-safe representation of CUDA device properties.

    Provides explicit types for `torch.cuda.get_device_properties()` return.
    The PyTorch stubs are incomplete, so we extract and type the values.

    Attributes:
        name: GPU device name (e.g., "NVIDIA GeForce RTX 4090")
        total_memory: Total memory in bytes
        major: Compute capability major version
        minor: Compute capability minor version
        multi_processor_count: Number of streaming multiprocessors
    """

    name: str
    total_memory: int
    major: int
    minor: int
    multi_processor_count: int


# Minimum compute capability for Tensor Core support
MIN_TENSOR_CORE_COMPUTE_CAPABILITY: Final[tuple[int, int]] = (7, 0)


def get_cuda_device_properties(device_index: int = 0) -> CudaDeviceProperties:
    """Get CUDA device properties with explicit typing.

    Wraps `torch.cuda.get_device_properties()` to provide fully-typed
    return value without relying on incomplete PyTorch stubs.

    Args:
        device_index: CUDA device index (default: 0)

    Returns:
        CudaDeviceProperties with all fields explicitly typed

    Raises:
        RuntimeError: If CUDA is not available or device index invalid
    """
    import torch

    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is not available")

    # Get properties from PyTorch (untyped return)
    props = torch.cuda.get_device_properties(device_index)

    # Extract and explicitly type each field
    return CudaDeviceProperties(
        name=str(props.name),
        total_memory=int(props.total_memory),
        major=int(props.major),
        minor=int(props.minor),
        multi_processor_count=int(props.multi_processor_count),
    )


def set_cuda_memory_fraction(fraction: float, device_index: int = 0) -> None:
    """Set CUDA per-process memory fraction with explicit typing.

    Wraps `torch.cuda.set_per_process_memory_fraction()` to provide
    explicit parameter types without relying on incomplete stubs.

    Args:
        fraction: Memory fraction (0.0-1.0)
        device_index: CUDA device index (default: 0)

    Raises:
        ValueError: If fraction is not in valid range
        RuntimeError: If CUDA is not available
    """
    import torch

    if not 0.0 <= fraction <= 1.0:
        raise ValueError(f"Memory fraction must be 0.0-1.0, got {fraction}")

    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is not available")

    # Call with explicitly typed arguments
    torch.cuda.set_per_process_memory_fraction(float(fraction), int(device_index))


def compile_model[T](
    model: T,
    *,
    mode: str = "reduce-overhead",
    fullgraph: bool = False,
) -> T:
    """Compile a PyTorch model with explicit typing.

    Wraps `torch.compile()` to provide fully-typed interface without
    relying on incomplete PyTorch stubs.

    Args:
        model: The model to compile (preserves type)
        mode: Compilation mode ('default', 'reduce-overhead', 'max-autotune')
        fullgraph: Whether to require full graph compilation

    Returns:
        Compiled model with same type as input

    Type Parameters:
        T: Model type (preserved through compilation)
    """
    import torch

    # torch.compile returns the same type as input
    return torch.compile(model, mode=mode, fullgraph=fullgraph)  # type: ignore[return-value]
