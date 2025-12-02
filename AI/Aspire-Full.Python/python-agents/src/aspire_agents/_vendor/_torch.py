"""PyTorch abstractions for type-safe GPU tensor operations.

Provides protocol definitions for torch module without requiring torch installation.
All torch operations are abstracted through these protocols.
"""

from __future__ import annotations

from collections.abc import Callable, Iterator, Sequence
from dataclasses import dataclass
from typing import (
    TYPE_CHECKING,
    Any,
    Final,
    Literal,
    Protocol,
    Self,
    TypeAlias,
    cast,
    override,
    runtime_checkable,
)

# ============================================================================
# Type Aliases
# ============================================================================

TorchDtype: TypeAlias = Literal[
    "float16",
    "float32",
    "float64",
    "bfloat16",
    "int8",
    "int16",
    "int32",
    "int64",
    "bool",
    "complex64",
    "complex128",
]

DeviceType: TypeAlias = Literal["cpu", "cuda", "mps"]


# ============================================================================
# Tensor Protocol
# ============================================================================


@runtime_checkable
class Tensor(Protocol):
    """Protocol for torch.Tensor operations.

    Provides type-safe interface for tensor operations without torch import.
    """

    @property
    def shape(self) -> tuple[int, ...]:
        """Tensor dimensions."""
        ...

    @property
    def dtype(self) -> Any:
        """Data type of tensor elements."""
        ...

    @property
    def device(self) -> "TorchDevice":
        """Device where tensor is stored."""
        ...

    @property
    def ndim(self) -> int:
        """Number of dimensions."""
        ...

    def size(self, dim: int | None = None) -> tuple[int, ...] | int:
        """Return tensor size."""
        ...

    def to(
        self,
        device: "TorchDevice | str | None" = None,
        dtype: Any = None,
        non_blocking: bool = False,
    ) -> Self:
        """Move tensor to device/dtype."""
        ...

    def cpu(self) -> Self:
        """Move to CPU."""
        ...

    def cuda(self, device: int | None = None) -> Self:
        """Move to CUDA device."""
        ...

    def detach(self) -> Self:
        """Detach from computation graph."""
        ...

    def clone(self) -> Self:
        """Create a copy."""
        ...

    def contiguous(self) -> Self:
        """Return contiguous tensor."""
        ...

    def unsqueeze(self, dim: int) -> Self:
        """Add dimension."""
        ...

    def squeeze(self, dim: int | None = None) -> Self:
        """Remove dimension."""
        ...

    def expand(self, *sizes: int) -> Self:
        """Expand to new size."""
        ...

    def float(self) -> Self:
        """Convert to float32."""
        ...

    def half(self) -> Self:
        """Convert to float16."""
        ...

    def __getitem__(self, key: Any) -> Self:
        """Index tensor."""
        ...

    def __add__(self, other: Any) -> Self:
        """Add tensors."""
        ...

    def __mul__(self, other: Any) -> Self:
        """Multiply tensors."""
        ...

    def __matmul__(self, other: "Tensor") -> Self:
        """Matrix multiply."""
        ...


# ============================================================================
# Device Protocol
# ============================================================================


@dataclass(frozen=True, slots=True)
class TorchDevice:
    """Representation of a torch device.

    Immutable device specification for type-safe device handling.
    """

    type: DeviceType
    index: int | None = None

    @override
    def __str__(self) -> str:
        if self.index is not None:
            return f"{self.type}:{self.index}"
        return self.type

    @classmethod
    def cuda(cls, index: int = 0) -> Self:
        """Create CUDA device."""
        return cls(type="cuda", index=index)

    @classmethod
    def cpu(cls) -> Self:
        """Create CPU device."""
        return cls(type="cpu", index=None)


# ============================================================================
# Module Protocol
# ============================================================================


@runtime_checkable
class TorchModule(Protocol):
    """Protocol for torch.nn.Module.

    Provides interface for neural network modules.
    """

    def __call__(self, *args: Any, **kwargs: Any) -> Any:
        """Forward pass."""
        ...

    def to(self, device: TorchDevice | str, dtype: Any = None) -> Self:
        """Move module to device."""
        ...

    def eval(self) -> Self:
        """Set to evaluation mode."""
        ...

    def train(self, mode: bool = True) -> Self:
        """Set to training mode."""
        ...

    def parameters(self, recurse: bool = True) -> Iterator[Tensor]:
        """Iterate over parameters."""
        ...

    def named_parameters(self, prefix: str = "", recurse: bool = True) -> Iterator[tuple[str, Tensor]]:
        """Iterate over named parameters."""
        ...

    def state_dict(self) -> dict[str, Any]:
        """Get state dictionary."""
        ...

    def load_state_dict(self, state_dict: dict[str, Any], strict: bool = True) -> Any:
        """Load state dictionary."""
        ...


# ============================================================================
# Optimizer Protocol
# ============================================================================


@runtime_checkable
class TorchOptimizer(Protocol):
    """Protocol for torch optimizers."""

    def zero_grad(self, set_to_none: bool = True) -> None:
        """Clear gradients."""
        ...

    def step(self, closure: Callable[[], float] | None = None) -> float | None:
        """Perform optimization step."""
        ...

    def state_dict(self) -> dict[str, Any]:
        """Get optimizer state."""
        ...

    def load_state_dict(self, state_dict: dict[str, Any]) -> None:
        """Load optimizer state."""
        ...


# ============================================================================
# CUDA Functions (abstracted)
# ============================================================================

# These are function stubs that will be replaced with real torch calls at runtime
_torch_available: bool = False
_torch: Any = None

if TYPE_CHECKING:
    import torch as _torch_import

    _torch = _torch_import
    _torch_available = True
else:
    try:
        import torch as _torch_import

        _torch = _torch_import
        _torch_available = True
    except ImportError:
        pass


def cuda_is_available() -> bool:
    """Check if CUDA is available."""
    if _torch_available:
        return bool(_torch.cuda.is_available())
    return False


def cuda_device_count() -> int:
    """Get number of CUDA devices."""
    if _torch_available:
        return int(_torch.cuda.device_count())
    return 0


def cuda_synchronize(device: int | None = None) -> None:
    """Synchronize CUDA stream."""
    if _torch_available:
        _torch.cuda.synchronize(device)


def cuda_empty_cache() -> None:
    """Empty CUDA cache."""
    if _torch_available:
        _torch.cuda.empty_cache()


_VALID_DEVICE_TYPES: Final[frozenset[str]] = frozenset({"cpu", "cuda", "mps"})


def get_device(device_str: str) -> TorchDevice:
    """Parse device string to TorchDevice.

    Args:
        device_str: Device string like "cuda", "cuda:0", "cpu", "mps".

    Returns:
        TorchDevice instance.

    Raises:
        ValueError: If device type is not recognized.
    """
    if ":" in device_str:
        dtype, idx = device_str.split(":")
        if dtype not in _VALID_DEVICE_TYPES:
            msg = f"Unknown device type: {dtype}"
            raise ValueError(msg)
        return TorchDevice(type=cast(DeviceType, dtype), index=int(idx))
    if device_str not in _VALID_DEVICE_TYPES:
        msg = f"Unknown device type: {device_str}"
        raise ValueError(msg)
    return TorchDevice(type=cast(DeviceType, device_str), index=None)


# ============================================================================
# Tensor Creation (abstracted)
# ============================================================================


def zeros(
    *size: int,
    dtype: TorchDtype = "float32",
    device: TorchDevice | str | None = None,
) -> Tensor:
    """Create zero tensor."""
    if not _torch_available:
        raise RuntimeError("PyTorch not available")
    dev = str(device) if device else None
    return _torch.zeros(*size, dtype=getattr(_torch, dtype), device=dev)


def ones(
    *size: int,
    dtype: TorchDtype = "float32",
    device: TorchDevice | str | None = None,
) -> Tensor:
    """Create ones tensor."""
    if not _torch_available:
        raise RuntimeError("PyTorch not available")
    dev = str(device) if device else None
    return _torch.ones(*size, dtype=getattr(_torch, dtype), device=dev)


def randn(
    *size: int,
    dtype: TorchDtype = "float32",
    device: TorchDevice | str | None = None,
) -> Tensor:
    """Create random normal tensor."""
    if not _torch_available:
        raise RuntimeError("PyTorch not available")
    dev = str(device) if device else None
    return _torch.randn(*size, dtype=getattr(_torch, dtype), device=dev)


def stack(tensors: Sequence[Tensor], dim: int = 0) -> Tensor:
    """Stack tensors along new dimension."""
    if not _torch_available:
        raise RuntimeError("PyTorch not available")
    return _torch.stack(list(tensors), dim=dim)


def cat(tensors: Sequence[Tensor], dim: int = 0) -> Tensor:
    """Concatenate tensors along dimension."""
    if not _torch_available:
        raise RuntimeError("PyTorch not available")
    return _torch.cat(list(tensors), dim=dim)


# ============================================================================
# Context Managers
# ============================================================================


class no_grad:
    """Context manager to disable gradient computation."""

    __slots__ = ("_prev",)

    def __init__(self) -> None:
        super().__init__()
        self._prev: bool = True

    def __enter__(self) -> Self:
        if _torch_available:
            self._prev = _torch.is_grad_enabled()
            _torch.set_grad_enabled(False)
        return self

    def __exit__(self, *args: Any) -> None:
        if _torch_available:
            _torch.set_grad_enabled(self._prev)


class autocast:
    """Context manager for automatic mixed precision."""

    __slots__ = ("device_type", "dtype", "enabled", "_ctx")

    def __init__(
        self,
        device_type: DeviceType = "cuda",
        dtype: TorchDtype = "float16",
        enabled: bool = True,
    ) -> None:
        super().__init__()
        self.device_type = device_type
        self.dtype = dtype
        self.enabled = enabled
        self._ctx: Any = None

    def __enter__(self) -> Self:
        if _torch_available and self.enabled:
            dtype_attr = getattr(_torch, self.dtype)
            self._ctx = _torch.autocast(
                device_type=self.device_type,
                dtype=dtype_attr,
            )
            self._ctx.__enter__()
        return self

    def __exit__(self, *args: Any) -> None:
        if self._ctx is not None:
            self._ctx.__exit__(*args)


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Types
    "Tensor",
    "TorchDevice",
    "TorchDtype",
    "TorchModule",
    "TorchOptimizer",
    "DeviceType",
    # CUDA functions
    "cuda_is_available",
    "cuda_device_count",
    "cuda_synchronize",
    "cuda_empty_cache",
    "get_device",
    # Tensor creation
    "zeros",
    "ones",
    "randn",
    "stack",
    "cat",
    # Context managers
    "no_grad",
    "autocast",
]
