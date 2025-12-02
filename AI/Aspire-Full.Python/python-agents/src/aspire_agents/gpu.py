"""GPU enforcement utilities for Python 3.15+ free-threaded runtime.

Provides Tensor Core GPU validation and PyTorch runtime configuration.
All functions are thread-safe and use functools.lru_cache for singleton
initialization pattern compatible with GIL-free execution.

Key Features:
- Validates compute capability >= 7.0 (Tensor Core support)
- Configures TF32/FP16 matrix math for Ampere+ GPUs
- Sets default device for automatic tensor placement
- Thread-safe via lru_cache (immune to GIL state)

Supported GPUs:
- Volta (V100): Compute 7.0+ - FP16 Tensor Cores
- Turing (RTX 20xx): Compute 7.5+ - FP16 Tensor Cores
- Ampere (A100, RTX 30xx): Compute 8.0+ - FP16/BF16/TF32 Tensor Cores
- Hopper (H100): Compute 9.0+ - FP8/FP16/BF16/TF32 Tensor Cores
"""

from __future__ import annotations

import logging
import sys
from dataclasses import dataclass
from functools import lru_cache
from typing import TYPE_CHECKING, Any, Final, cast

if TYPE_CHECKING:
    import torch as torch_module

# Lazy torch import with proper fallback
try:
    import torch  # type: ignore[import-untyped]
except ImportError:
    torch = None  # type: ignore[assignment]

logger: Final[logging.Logger] = logging.getLogger(__name__)


class TensorCoreUnavailableError(RuntimeError):
    """Raised when the environment cannot satisfy the GPU requirements.

    This error indicates that:
    - No CUDA-capable GPU is detected, OR
    - The GPU lacks Tensor Core support (compute capability < 7.0), OR
    - PyTorch CUDA is not installed
    """


@dataclass(frozen=True, slots=True)
class TensorCoreInfo:
    """Immutable metadata about the active CUDA device.

    All fields are computed once during initialization and cached.
    Thread-safe due to immutability (frozen=True) and __slots__.

    Attributes:
        name: GPU model name (e.g., "NVIDIA GeForce RTX 4090")
        compute_capability: Compute capability string (e.g., "8.9")
        total_memory_gb: Total GPU memory in gigabytes
        device_index: CUDA device index (usually 0)
        tf32_enabled: Whether TF32 matrix math is enabled
        cudnn_tf32_enabled: Whether cuDNN TF32 is enabled
        gil_disabled: Whether Python GIL is disabled (3.15+ free-threaded)
    """

    name: str
    compute_capability: str
    total_memory_gb: float
    device_index: int
    tf32_enabled: bool = True
    cudnn_tf32_enabled: bool = True
    gil_disabled: bool = False

    @property
    def supports_fp16(self) -> bool:
        """Check if device supports efficient FP16 Tensor Core operations.

        Requires Volta+ (compute capability 7.0+).
        """
        major = int(self.compute_capability.split(".")[0])
        return major >= 7

    @property
    def supports_bf16(self) -> bool:
        """Check if device supports BF16 Tensor Core operations.

        Requires Ampere+ (compute capability 8.0+).
        """
        major = int(self.compute_capability.split(".")[0])
        return major >= 8

    @property
    def supports_fp8(self) -> bool:
        """Check if device supports FP8 Tensor Core operations.

        Requires Hopper+ (compute capability 9.0+).
        """
        major = int(self.compute_capability.split(".")[0])
        return major >= 9

    @property
    def tensor_core_generation(self) -> str:
        """Get the Tensor Core generation name."""
        major = int(self.compute_capability.split(".")[0])
        generations = {
            7: "Volta/Turing (1st/2nd gen)",
            8: "Ampere (3rd gen)",
            9: "Hopper (4th gen)",
        }
        return generations.get(major, f"Unknown (cc {major}.x)")


def _format_mem(bytes_total: int) -> float:
    """Format bytes to GB with 2 decimal places."""
    return round(bytes_total / (1024**3), 2)


def _is_gil_disabled() -> bool:
    """Check if Python GIL is disabled (Python 3.15+ free-threaded).

    Returns:
        True if running in free-threaded mode (PYTHON_GIL=0)
    """
    if hasattr(sys, "_is_gil_enabled"):
        return not sys._is_gil_enabled()  # type: ignore[attr-defined]
    return False


def _configure_torch_runtime(torch_mod: Any, device_index: int) -> tuple[bool, bool]:
    """Configure PyTorch for optimal Tensor Core utilization.

    Enables TF32 math for float32 operations (2-3x speedup on Ampere+).
    Sets default device for automatic tensor placement.

    Args:
        torch_mod: The torch module
        device_index: CUDA device index

    Returns:
        Tuple of (tf32_enabled, cudnn_tf32_enabled)
    """
    # Set active CUDA device
    torch_mod.cuda.set_device(device_index)

    # Enable TensorFloat-32 (TF32) for float32 matrix operations
    # Provides ~2-3x speedup on Ampere+ with minimal precision loss
    tf32_enabled = True
    cudnn_tf32_enabled = True
    torch_mod.backends.cuda.matmul.allow_tf32 = True
    torch_mod.backends.cudnn.allow_tf32 = True

    # Set default device for automatic tensor placement (torch 2.0+)
    try:
        torch_mod.set_default_device(f"cuda:{device_index}")
        logger.debug("Set default torch device to cuda:%d", device_index)
    except AttributeError:
        pass  # Legacy torch version

    # Set matmul precision to 'high' for best Tensor Core usage
    try:
        torch_mod.set_float32_matmul_precision("high")
        logger.debug("Set float32 matmul precision to 'high'")
    except AttributeError:
        pass  # Legacy torch version

    # Enable oneDNN JIT fusion for better kernel efficiency (CPU fallback)
    try:
        torch_mod.jit.enable_onednn_fusion(True)
    except (AttributeError, RuntimeError):
        pass

    # Configure memory allocator for reduced fragmentation
    try:
        torch_mod.cuda.memory.set_per_process_memory_fraction(0.95, device_index)
    except (AttributeError, RuntimeError):
        pass

    return tf32_enabled, cudnn_tf32_enabled


@lru_cache(maxsize=1)
def ensure_tensor_core_gpu() -> TensorCoreInfo:
    """Validate and configure a Tensor Core capable GPU.

    Thread-safe: lru_cache ensures single initialization even with GIL disabled.
    The cache is immune to race conditions in Python 3.15 free-threaded mode.

    This function:
    1. Validates PyTorch CUDA is available
    2. Checks compute capability >= 7.0 (Tensor Core requirement)
    3. Configures TF32/FP16 matrix math
    4. Sets default CUDA device

    Returns:
        TensorCoreInfo with device metadata and configuration state.

    Raises:
        TensorCoreUnavailableError: If no suitable GPU is found

    Examples:
        >>> info = ensure_tensor_core_gpu()
        >>> print(f"Using {info.name} with {info.total_memory_gb}GB")
    """
    if torch is None:
        raise TensorCoreUnavailableError(
            "PyTorch with CUDA support is required. Run `uv pip install .` from "
            "python-agents/ after ensuring CUDA wheels are available."
        )

    if not torch.cuda.is_available():
        raise TensorCoreUnavailableError(
            "CUDA GPU not detected. Ensure the devcontainer/host exposes an NVIDIA device. "
            "Check: nvidia-smi, NVIDIA_VISIBLE_DEVICES env var, Docker --gpus flag."
        )

    device_index = 0
    # Cast torch to Any to avoid partial type errors from the library stubs
    torch_any = cast(Any, torch)
    props = torch_any.cuda.get_device_properties(device_index)
    total_memory: int = props.total_memory
    gpu_name: str = props.name
    major, minor = torch.cuda.get_device_capability(device_index)
    capability_str = f"{major}.{minor}"

    if major < 7:
        raise TensorCoreUnavailableError(
            f"Detected GPU '{gpu_name}' lacks Tensor Cores "
            f"(compute capability {capability_str} < 7.0). "
            f"Minimum requirement: Volta V100 or newer."
        )

    tf32_enabled, cudnn_tf32_enabled = _configure_torch_runtime(torch, device_index)
    gil_disabled = _is_gil_disabled()

    info = TensorCoreInfo(
        name=gpu_name,
        compute_capability=capability_str,
        total_memory_gb=_format_mem(total_memory),
        device_index=device_index,
        tf32_enabled=tf32_enabled,
        cudnn_tf32_enabled=cudnn_tf32_enabled,
        gil_disabled=gil_disabled,
    )

    logger.info(
        "Tensor Core GPU provisioned: %s (Compute %s, %.2f GB, TF32=%s, GIL=%s)",
        info.name,
        info.compute_capability,
        info.total_memory_gb,
        "enabled" if info.tf32_enabled else "disabled",
        "disabled" if info.gil_disabled else "enabled",
    )

    return info


def get_gpu_memory_info() -> dict[str, float]:
    """Get current GPU memory usage statistics.

    Returns:
        Dictionary with allocated_gb, reserved_gb, total_gb, free_gb

    Raises:
        TensorCoreUnavailableError: If no GPU available
    """
    if torch is None or not torch.cuda.is_available():
        raise TensorCoreUnavailableError("No GPU available for memory stats")

    torch_any = cast(Any, torch)
    allocated = torch_any.cuda.memory_allocated()
    reserved = torch_any.cuda.memory_reserved()
    total = torch_any.cuda.get_device_properties(0).total_memory

    return {
        "allocated_gb": _format_mem(allocated),
        "reserved_gb": _format_mem(reserved),
        "total_gb": _format_mem(total),
        "free_gb": _format_mem(total - allocated),
    }
