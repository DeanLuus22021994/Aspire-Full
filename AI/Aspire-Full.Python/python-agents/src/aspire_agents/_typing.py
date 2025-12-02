"""Type definitions and runtime utilities for Python 3.15+ free-threading.

This module provides:
- Type-safe wrappers for PyTorch CUDA functions
- Free-threading detection via public Python 3.15+ APIs
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
# Free-Threading Detection (Python 3.15+ public API)
# ============================================================================


def is_gil_disabled() -> bool:
    """Check if Python GIL is disabled using public API.

    Python 3.15+ provides `sys.flags.free_threading` as the public API
    for detecting free-threaded builds. The private `sys._is_gil_enabled()`
    should not be used in type-checked code.

    Returns:
        True if running in free-threaded mode (PYTHON_GIL=0)

    Examples:
        >>> if is_gil_disabled():
        ...     print("Running in free-threaded mode")
    """
    # Python 3.15+ public API via sys.flags
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


def _extract_device_props(props: object) -> CudaDeviceProperties:
    """Extract typed properties from PyTorch device properties object.

    Args:
        props: The object returned by torch.cuda.get_device_properties()

    Returns:
        CudaDeviceProperties with all fields explicitly typed
    """
    return CudaDeviceProperties(
        name=str(getattr(props, "name", "")),
        total_memory=int(getattr(props, "total_memory", 0)),
        major=int(getattr(props, "major", 0)),
        minor=int(getattr(props, "minor", 0)),
        multi_processor_count=int(getattr(props, "multi_processor_count", 0)),
    )


def get_cuda_device_properties(device_index: int = 0) -> CudaDeviceProperties:
    """Get CUDA device properties with explicit typing.

    Wraps `torch.cuda.get_device_properties()` to provide fully-typed
    return value.

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

    # Use getattr to get the function and call it - avoids Unknown return type issue
    get_props = getattr(torch.cuda, "get_device_properties")
    props: object = get_props(device_index)
    return _extract_device_props(props)


def set_cuda_memory_fraction(fraction: float, device_index: int = 0) -> None:
    """Set CUDA per-process memory fraction with explicit typing.

    Wraps `torch.cuda.set_per_process_memory_fraction()` to provide
    explicit parameter types.

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

    # Use getattr to call the function to avoid type checker issues with Unknown param
    set_mem_frac = getattr(torch.cuda, "set_per_process_memory_fraction")
    set_mem_frac(fraction, device_index)


def compile_model(model: object, *, mode: str = "reduce-overhead", fullgraph: bool = False) -> object:
    """Compile a PyTorch model with torch.compile.

    Wraps `torch.compile()` to provide typed interface.

    Args:
        model: The model to compile
        mode: Compilation mode ('default', 'reduce-overhead', 'max-autotune')
        fullgraph: Whether to require full graph compilation

    Returns:
        Compiled model
    """
    import torch

    # Use getattr to get compile function to avoid overload resolution issues
    compile_fn = getattr(torch, "compile")
    return compile_fn(model, mode=mode, fullgraph=fullgraph)
