"""GPU enforcement utilities for Aspire python agents."""

from __future__ import annotations

from dataclasses import dataclass
from functools import lru_cache


class TensorCoreUnavailableError(RuntimeError):
    """Raised when the environment cannot satisfy the GPU requirements."""


@dataclass(frozen=True, slots=True)
class TensorCoreInfo:
    """Metadata about the active CUDA device."""

    name: str
    compute_capability: str
    total_memory_gb: float


def _format_mem(bytes_total: int) -> float:
    return round(bytes_total / (1024**3), 2)


@lru_cache(maxsize=1)
def ensure_tensor_core_gpu() -> TensorCoreInfo:
    """Validate that torch sees a Tensor Core capable GPU and return its metadata."""

    try:
        import torch  # type: ignore
    except ImportError as exc:  # pragma: no cover - environment guard
        raise TensorCoreUnavailableError(
            "PyTorch with CUDA support is required. Run `uv pip install .` from "
            "python-agents/ after ensuring CUDA wheels are available."
        ) from exc

    if not torch.cuda.is_available():  # pragma: no cover - runtime env check
        raise TensorCoreUnavailableError(
            "CUDA GPU not detected. Ensure the devcontainer/host exposes an NVIDIA device."
        )

    device_index = 0
    props = torch.cuda.get_device_properties(device_index)
    total_memory = props.total_memory
    gpu_name = props.name
    major, minor = torch.cuda.get_device_capability(device_index)
    capability_str = f"{major}.{minor}"
    if major < 7:
        raise TensorCoreUnavailableError(
            "Detected GPU lacks Tensor Cores (compute capability < 7.0)."
        )

    return TensorCoreInfo(
        name=gpu_name,
        compute_capability=capability_str,
        total_memory_gb=_format_mem(total_memory),
    )
