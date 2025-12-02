"""GPU enforcement utilities for Python 3.15+ free-threaded runtime.

Provides Tensor Core GPU validation and PyTorch runtime configuration.
All functions are thread-safe and use functools.lru_cache for singleton
initialization pattern compatible with GIL-free execution.

Key Features:
- Validates compute capability >= 7.0 (Tensor Core support)
- Configures TF32/FP16 matrix math for Ampere+ GPUs
- Sets default device for automatic tensor placement
- Thread-safe via lru_cache (immune to GIL state)
"""

from __future__ import annotations

import logging
import sys
from dataclasses import dataclass
from functools import lru_cache
from typing import TYPE_CHECKING, Any, Final, cast

if TYPE_CHECKING:
    import torch as torch_module

try:  # pylint: disable=import-error
    import torch  # type: ignore
except ImportError:  # pragma: no cover - torch not installed yet
    torch = None  # type: ignore[assignment]


logger: Final = logging.getLogger(__name__)


class TensorCoreUnavailableError(RuntimeError):
    """Raised when the environment cannot satisfy the GPU requirements."""


@dataclass(frozen=True, slots=True)
class TensorCoreInfo:
    """Immutable metadata about the active CUDA device.

    All fields are computed once during initialization and cached.
    Thread-safe due to immutability (frozen=True).
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
        """Check if device supports efficient FP16 operations."""
        major = int(self.compute_capability.split(".")[0])
        return major >= 7

    @property
    def supports_bf16(self) -> bool:
        """Check if device supports BF16 (Ampere+)."""
        major = int(self.compute_capability.split(".")[0])
        return major >= 8


def _format_mem(bytes_total: int) -> float:
    """Format bytes to GB with 2 decimal places."""
    return round(bytes_total / (1024**3), 2)


def _is_gil_disabled() -> bool:
    """Check if Python GIL is disabled (Python 3.15+ free-threaded)."""
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
    except AttributeError:  # pragma: no cover - legacy fallback
        pass

    # Set matmul precision to 'high' for best Tensor Core usage
    try:
        torch_mod.set_float32_matmul_precision("high")
    except AttributeError:  # pragma: no cover - legacy fallback
        pass

    # Enable JIT fusion for better kernel efficiency
    try:
        torch_mod.jit.enable_onednn_fusion(True)
    except (AttributeError, RuntimeError):  # pragma: no cover
        pass

    return tf32_enabled, cudnn_tf32_enabled


@lru_cache(maxsize=1)
def ensure_tensor_core_gpu() -> TensorCoreInfo:
    """Validate and configure a Tensor Core capable GPU.

    Thread-safe: lru_cache ensures single initialization even with GIL disabled.
    Raises TensorCoreUnavailableError if no suitable GPU is found.

    Returns:
        TensorCoreInfo with device metadata and configuration state.
    """
    if torch is None:  # pragma: no cover - environment guard
        raise TensorCoreUnavailableError(
            "PyTorch with CUDA support is required. Run `uv pip install .` from "
            "python-agents/ after ensuring CUDA wheels are available."
        )

    if not torch.cuda.is_available():  # pragma: no cover - runtime env check
        raise TensorCoreUnavailableError(
            "CUDA GPU not detected. Ensure the devcontainer/host exposes an NVIDIA device."
        )

    device_index = 0
    # Cast torch to Any to avoid partial type errors from the library
    torch_any = cast(Any, torch)
    props = torch_any.cuda.get_device_properties(device_index)
    total_memory = props.total_memory
    gpu_name = props.name
    major, minor = torch.cuda.get_device_capability(device_index)
    capability_str = f"{major}.{minor}"

    if major < 7:
        raise TensorCoreUnavailableError(
            f"Detected GPU '{gpu_name}' lacks Tensor Cores (compute capability {capability_str} < 7.0)."
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
        "Tensor Core GPU provisioned: %s (Compute %s, %s GB, TF32=%s, GIL=%s)",
        info.name,
        info.compute_capability,
        info.total_memory_gb,
        "enabled" if info.tf32_enabled else "disabled",
        "disabled" if info.gil_disabled else "enabled",
    )
    return info
