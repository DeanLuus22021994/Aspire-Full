"""Safetensors vendor abstractions.

Provides protocol definitions for the safetensors library,
enabling type checking without requiring the package to be installed.

Safetensors is a file format for storing tensors safely and efficiently.
Key features:
- Safe deserialization (no arbitrary code execution)
- Efficient memory-mapped loading
- Metadata support
- Multi-framework support (PyTorch, TensorFlow, NumPy, etc.)
"""

from __future__ import annotations

from collections.abc import Iterator
from types import TracebackType
from typing import (
    Any,
    Final,
    Protocol,
    TypeVar,
    runtime_checkable,
)

# ============================================================================
# Type Variables
# ============================================================================

T = TypeVar("T")
TensorT = TypeVar("TensorT")


# ============================================================================
# Exceptions
# ============================================================================


class SafetensorError(Exception):
    """Base exception for safetensors operations.

    Raised when safetensors encounters an error during:
    - File parsing
    - Tensor deserialization
    - Validation failures
    """

    pass


# ============================================================================
# Safe Open Protocol
# ============================================================================


@runtime_checkable
class SafeOpenProtocol(Protocol):
    """Protocol for safetensors.safe_open context manager.

    Provides safe, memory-mapped access to tensor files.
    Supports lazy loading of individual tensors.

    Example usage:
        with safe_open("model.safetensors", framework="pt") as f:
            tensor = f.get_tensor("layer.weight")
            keys = f.keys()
            metadata = f.metadata()
    """

    def __init__(
        self,
        filename: str,
        framework: str,
        device: str | None = None,
    ) -> None:
        """Initialize safe_open context.

        Args:
            filename: Path to the safetensors file
            framework: Target framework ("pt", "tf", "np", "jax", "paddle")
            device: Optional device for tensor loading (e.g., "cpu", "cuda:0")
        """
        super().__init__()

    def __enter__(self) -> "SafeOpenProtocol":
        """Enter context manager.

        Returns:
            Self for use in with statement
        """
        ...

    def __exit__(
        self,
        __exc_type: type[BaseException] | None,
        __exc_val: BaseException | None,
        __exc_tb: TracebackType | None,
        /,
    ) -> None:
        """Exit context manager and release resources."""
        ...

    def get_tensor(self, name: str) -> Any:
        """Get a tensor by name.

        Args:
            name: Name of the tensor to retrieve

        Returns:
            Tensor in the target framework format

        Raises:
            SafetensorError: If tensor not found or load fails
        """
        ...

    def get_slice(self, name: str) -> Any:
        """Get a tensor slice for partial loading.

        Args:
            name: Name of the tensor

        Returns:
            Slice object for efficient partial tensor access
        """
        ...

    def keys(self) -> Iterator[str]:
        """Get iterator of tensor names in the file.

        Returns:
            Iterator over tensor name strings
        """
        ...

    def metadata(self) -> dict[str, str] | None:
        """Get file metadata.

        Returns:
            Dictionary of metadata key-value pairs, or None
        """
        ...

    def offset_keys(self) -> dict[str, tuple[int, int]]:
        """Get tensor byte offsets in the file.

        Returns:
            Dictionary mapping tensor names to (start, end) byte offsets
        """
        ...


# ============================================================================
# Serialization Functions
# ============================================================================


def serialize(
    tensor_dict: dict[str, Any],
    metadata: dict[str, str] | None = None,
) -> bytes:
    """Serialize tensors to safetensors bytes.

    Args:
        tensor_dict: Dictionary mapping names to tensors
        metadata: Optional metadata dictionary

    Returns:
        Serialized bytes in safetensors format

    Raises:
        SafetensorError: If serialization fails
    """
    ...


def deserialize(data: bytes) -> dict[str, Any]:
    """Deserialize safetensors bytes to tensors.

    Args:
        data: Bytes in safetensors format

    Returns:
        Dictionary mapping tensor names to tensors

    Raises:
        SafetensorError: If deserialization fails
    """
    ...


def serialize_file(
    tensor_dict: dict[str, Any],
    filename: str,
    metadata: dict[str, str] | None = None,
) -> None:
    """Serialize tensors directly to a file.

    Args:
        tensor_dict: Dictionary mapping names to tensors
        filename: Output file path
        metadata: Optional metadata dictionary

    Raises:
        SafetensorError: If serialization fails
        IOError: If file write fails
    """
    ...


# ============================================================================
# Helper Functions
# ============================================================================


def load_file(
    filename: str,
    framework: str = "pt",
    device: str | None = None,
) -> dict[str, Any]:
    """Load all tensors from a safetensors file.

    Convenience function that opens and loads all tensors.

    Args:
        filename: Path to safetensors file
        framework: Target framework ("pt", "tf", "np", "jax")
        device: Optional device for loading

    Returns:
        Dictionary mapping tensor names to tensors
    """
    ...


def save_file(
    tensors: dict[str, Any],
    filename: str,
    metadata: dict[str, str] | None = None,
) -> None:
    """Save tensors to a safetensors file.

    Convenience function for saving tensor dictionaries.

    Args:
        tensors: Dictionary mapping names to tensors
        filename: Output file path
        metadata: Optional metadata dictionary
    """
    ...


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Exceptions
    "SafetensorError",
    # Protocols
    "SafeOpenProtocol",
    # Serialization Functions
    "serialize",
    "deserialize",
    "serialize_file",
    # Helper Functions
    "load_file",
    "save_file",
]
