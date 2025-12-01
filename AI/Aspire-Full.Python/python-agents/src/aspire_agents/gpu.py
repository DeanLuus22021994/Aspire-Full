"""GPU enforcement utilities for Aspire python agents."""

from __future__ import annotations

import logging
from dataclasses import dataclass
from functools import lru_cache
from typing import Any, cast

try:  # pylint: disable=import-error
    import torch  # type: ignore
except ImportError:  # pragma: no cover - torch not installed yet
    torch = None  # type: ignore[assignment]


logger = logging.getLogger(__name__)


class TensorCoreUnavailableError(RuntimeError):
    """Raised when the environment cannot satisfy the GPU requirements."""


@dataclass(frozen=True, slots=True)
class TensorCoreInfo:
    """Metadata about the active CUDA device."""

    name: str
    compute_capability: str
    total_memory_gb: float
    device_index: int


def _format_mem(bytes_total: int) -> float:
    return round(bytes_total / (1024**3), 2)


def _configure_torch_runtime(torch_mod: Any, device_index: int) -> None:
    """Pin PyTorch to the requested device and enable Tensor Core math optimizations."""

    torch_mod.cuda.set_device(device_index)

    # Leverage Tensor Core friendly defaults when available.
    torch_mod.backends.cuda.matmul.allow_tf32 = True
    torch_mod.backends.cudnn.allow_tf32 = True
    try:  # torch>=2.0
        torch_mod.set_default_device(f"cuda:{device_index}")
    except AttributeError:  # pragma: no cover - legacy fallback
        pass
    try:
        torch_mod.set_float32_matmul_precision("high")
    except AttributeError:  # pragma: no cover - legacy fallback
        pass


@lru_cache(maxsize=1)
def ensure_tensor_core_gpu() -> TensorCoreInfo:
    """Validate that torch sees a Tensor Core capable GPU and return its metadata."""

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
            "Detected GPU lacks Tensor Cores (compute capability < 7.0)."
        )

    _configure_torch_runtime(torch, device_index)

    info = TensorCoreInfo(
        name=gpu_name,
        compute_capability=capability_str,
        total_memory_gb=_format_mem(total_memory),
        device_index=device_index,
    )

    logger.info(
        "Tensor Core GPU provisioned: %s (Compute %s, %s GB)",
        info.name,
        info.compute_capability,
        info.total_memory_gb,
    )
    return info
