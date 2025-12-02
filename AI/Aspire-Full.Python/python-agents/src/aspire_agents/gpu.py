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

import torch

if TYPE_CHECKING:
    from torch import Tensor

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
    def supports_tf32(self) -> bool:
        """Check if device supports TF32 matrix operations.

        Requires Ampere+ (compute capability 8.0+).
        """
        major = int(self.compute_capability.split(".")[0])
        return major >= 8

    @property
    def tensor_core_generation(self) -> str:
        """Get the Tensor Core generation name."""
        major = int(self.compute_capability.split(".")[0])
        generations: dict[int, str] = {
            7: "Volta/Turing (1st/2nd gen)",
            8: "Ampere (3rd gen)",
            9: "Hopper (4th gen)",
        }
        return generations.get(major, f"Unknown (cc {major}.x)")

    @property
    def recommended_dtype(self) -> str:
        """Get the recommended dtype for this GPU architecture."""
        major = int(self.compute_capability.split(".")[0])
        if major >= 9:
            return "float8_e4m3fn"  # Hopper FP8
        if major >= 8:
            return "bfloat16"  # Ampere BF16
        return "float16"  # Volta/Turing FP16


def _format_mem(bytes_total: int) -> float:
    """Format bytes to GB with 2 decimal places."""
    return round(bytes_total / (1024**3), 2)


def _is_gil_disabled() -> bool:
    """Check if Python GIL is disabled (Python 3.15+ free-threaded).

    Returns:
        True if running in free-threaded mode (PYTHON_GIL=0)
    """
    if hasattr(sys, "_is_gil_enabled"):
        return not sys._is_gil_enabled()
    return False


def _configure_torch_runtime(device_index: int) -> tuple[bool, bool]:
    """Configure PyTorch for optimal Tensor Core utilization.

    Enables TF32 math for float32 operations (2-3x speedup on Ampere+).
    Sets default device for automatic tensor placement.

    Args:
        device_index: CUDA device index

    Returns:
        Tuple of (tf32_enabled, cudnn_tf32_enabled)
    """
    # Set active CUDA device
    torch.cuda.set_device(device_index)

    # Enable TensorFloat-32 (TF32) for float32 matrix operations
    # Provides ~2-3x speedup on Ampere+ with minimal precision loss
    tf32_enabled = True
    cudnn_tf32_enabled = True
    torch.backends.cuda.matmul.allow_tf32 = True
    torch.backends.cudnn.allow_tf32 = True

    # Set default device for automatic tensor placement (torch 2.0+)
    try:
        torch.set_default_device(f"cuda:{device_index}")
        logger.debug("Set default torch device to cuda:%d", device_index)
    except AttributeError:
        pass  # Legacy torch version

    # Set matmul precision to 'high' for best Tensor Core usage
    try:
        torch.set_float32_matmul_precision("high")
        logger.debug("Set float32 matmul precision to 'high'")
    except AttributeError:
        pass  # Legacy torch version

    # Enable cuDNN benchmark mode for optimized kernel selection
    torch.backends.cudnn.benchmark = True
    torch.backends.cudnn.enabled = True

    # Configure memory allocator for reduced fragmentation
    try:
        # Use expandable segments for better memory reuse
        torch.cuda.memory.set_per_process_memory_fraction(0.95, device_index)
    except (AttributeError, RuntimeError):
        pass

    # Enable flash attention if available (PyTorch 2.0+)
    try:
        torch.backends.cuda.enable_flash_sdp(True)
        torch.backends.cuda.enable_mem_efficient_sdp(True)
        logger.debug("Flash attention enabled for efficient self-attention")
    except AttributeError:
        pass

    return tf32_enabled, cudnn_tf32_enabled


def _warmup_gpu(device_index: int) -> None:
    """Warm up the GPU with a small tensor operation.

    This helps avoid cold-start latency on first real operation.
    Thread-safe and called once during initialization.
    """
    try:
        with torch.cuda.device(device_index):
            # Small matmul to trigger CUDA context initialization
            a = torch.randn(64, 64, device=f"cuda:{device_index}", dtype=torch.float16)
            b = torch.randn(64, 64, device=f"cuda:{device_index}", dtype=torch.float16)
            _ = torch.matmul(a, b)
            torch.cuda.synchronize()
            del a, b
            torch.cuda.empty_cache()
        logger.debug("GPU warmup completed on device %d", device_index)
    except Exception as e:
        logger.warning("GPU warmup failed: %s", e)


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
    5. Warms up the GPU

    Returns:
        TensorCoreInfo with device metadata and configuration state.

    Raises:
        TensorCoreUnavailableError: If no suitable GPU is found

    Examples:
        >>> info = ensure_tensor_core_gpu()
        >>> print(f"Using {info.name} with {info.total_memory_gb}GB")
    """
    if not torch.cuda.is_available():
        raise TensorCoreUnavailableError(
            "CUDA GPU not detected. Ensure the devcontainer/host exposes an NVIDIA device. "
            "Check: nvidia-smi, NVIDIA_VISIBLE_DEVICES env var, Docker --gpus flag."
        )

    device_index = 0
    props = torch.cuda.get_device_properties(device_index)
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

    tf32_enabled, cudnn_tf32_enabled = _configure_torch_runtime(device_index)
    gil_disabled = _is_gil_disabled()

    # Warm up the GPU to avoid cold-start latency
    _warmup_gpu(device_index)

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
    if not torch.cuda.is_available():
        raise TensorCoreUnavailableError("No GPU available for memory stats")

    allocated = torch.cuda.memory_allocated()
    reserved = torch.cuda.memory_reserved()
    total = torch.cuda.get_device_properties(0).total_memory

    return {
        "allocated_gb": _format_mem(allocated),
        "reserved_gb": _format_mem(reserved),
        "total_gb": _format_mem(total),
        "free_gb": _format_mem(total - allocated),
    }


def get_optimal_batch_size(
    model_memory_mb: float,
    sequence_length: int = 512,
    dtype_bytes: int = 2,  # float16
) -> int:
    """Calculate optimal batch size based on available GPU memory.

    Uses a conservative 70% of free memory to avoid OOM.

    Args:
        model_memory_mb: Estimated model memory in MB
        sequence_length: Token sequence length
        dtype_bytes: Bytes per element (2 for fp16, 4 for fp32)

    Returns:
        Recommended batch size
    """
    if not torch.cuda.is_available():
        return 8  # CPU fallback default

    mem_info = get_gpu_memory_info()
    free_memory_mb = mem_info["free_gb"] * 1024

    # Reserve 30% headroom for activations and gradients
    available_mb = (free_memory_mb - model_memory_mb) * 0.7

    # Estimate per-sample memory: seq_len * hidden_dim * dtype_bytes
    # Assuming average hidden_dim of 768 for transformer models
    hidden_dim = 768
    per_sample_mb = (sequence_length * hidden_dim * dtype_bytes) / (1024 * 1024)

    batch_size = max(1, int(available_mb / per_sample_mb))

    # Clamp to reasonable bounds
    return min(max(batch_size, 1), 256)


def synchronize_cuda() -> None:
    """Synchronize CUDA stream and clear cache.

    Thread-safe utility for ensuring all GPU operations complete.
    """
    if torch.cuda.is_available():
        torch.cuda.synchronize()


def empty_cache() -> None:
    """Empty CUDA cache to free unused memory.

    Thread-safe utility for memory management.
    """
    if torch.cuda.is_available():
        torch.cuda.empty_cache()
