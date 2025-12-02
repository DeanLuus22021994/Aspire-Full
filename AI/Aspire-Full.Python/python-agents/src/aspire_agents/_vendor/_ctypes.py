"""C FFI (ctypes) vendor abstractions.

Provides protocol definitions for the ctypes module,
enabling type checking for C interop code.

ctypes is Python's foreign function library for:
- Loading shared libraries (DLLs/.so files)
- Calling C functions
- Accessing C data structures
- Creating C-compatible data types
"""

from __future__ import annotations

from collections.abc import Buffer, Iterator
from typing import (
    Any,
    Final,
    Generic,
    Protocol,
    TypeVar,
    overload,
    override,
    runtime_checkable,
)

# ============================================================================
# Type Variables
# ============================================================================

T = TypeVar("T")
CT = TypeVar("CT", bound="CDataProtocol")
CT_co = TypeVar("CT_co", bound="CDataProtocol", covariant=True)


# ============================================================================
# Base CData Protocol
# ============================================================================


@runtime_checkable
class CDataProtocol(Protocol):
    """Base protocol for all ctypes data types.

    All ctypes types inherit from _CData and share these methods.
    """

    @property
    def _b_base_(self) -> int:
        """Base address of the underlying buffer."""
        ...

    @property
    def _b_needsfree_(self) -> bool:
        """Whether the buffer needs to be freed."""
        ...

    @property
    def _objects(self) -> dict[Any, int] | None:
        """Objects referenced by this data structure."""
        ...

    @classmethod
    def from_address(cls: type[CT], address: int) -> CT:
        """Create instance from a memory address.

        Args:
            address: Memory address to wrap

        Returns:
            CData instance pointing to address

        Warning:
            Unsafe - can crash if address is invalid!
        """
        ...

    @classmethod
    def from_buffer(
        cls: type[CT],
        source: Buffer,
        offset: int = 0,
    ) -> CT:
        """Create instance from a buffer object.

        Args:
            source: Buffer-supporting object
            offset: Byte offset into buffer

        Returns:
            CData instance sharing buffer memory
        """
        ...

    @classmethod
    def from_buffer_copy(
        cls: type[CT],
        source: Buffer,
        offset: int = 0,
    ) -> CT:
        """Create instance by copying from a buffer.

        Args:
            source: Buffer-supporting object
            offset: Byte offset into buffer

        Returns:
            CData instance with copied data
        """
        ...

    @classmethod
    def in_dll(cls: type[CT], library: "CDLLProtocol", name: str) -> CT:
        """Access a symbol exported by a shared library.

        Args:
            library: Loaded shared library
            name: Name of the exported symbol

        Returns:
            CData instance pointing to the symbol
        """
        ...

    def __buffer__(self, flags: int) -> memoryview:
        """Get a memory view of the data.

        Args:
            flags: Buffer flags

        Returns:
            Memory view of the underlying buffer
        """
        ...

    def __release_buffer__(self, view: memoryview) -> None:
        """Release a memory view.

        Args:
            view: Memory view to release
        """
        ...


# ============================================================================
# Simple C Data Protocol
# ============================================================================


@runtime_checkable
class SimpleCDataProtocol(CDataProtocol, Protocol, Generic[T]):
    """Protocol for simple C types (c_int, c_float, c_char_p, etc.).

    Simple types wrap a single value and provide .value access.
    """

    @property
    def value(self) -> T:
        """The Python value of the C data."""
        ...

    @value.setter
    def value(self, val: T) -> None:
        """Set the Python value."""
        ...


# ============================================================================
# Structure and Union Protocols
# ============================================================================


@runtime_checkable
class StructureProtocol(CDataProtocol, Protocol):
    """Protocol for ctypes.Structure.

    Represents a C struct with named fields.

    Example:
        class Point(Structure):
            _fields_ = [("x", c_int), ("y", c_int)]
    """

    # Class attributes
    _fields_: list[tuple[str, type] | tuple[str, type, int]]
    """Field definitions: (name, type) or (name, type, bitwidth)."""

    _pack_: int
    """Alignment in bytes (1, 2, 4, 8)."""

    _anonymous_: list[str]
    """Anonymous union/struct field names."""

    def __getattr__(self, name: str) -> Any:
        """Get field value by name."""
        ...

    @override
    def __setattr__(self, name: str, value: object) -> None:
        """Set field value by name."""
        ...


@runtime_checkable
class UnionProtocol(CDataProtocol, Protocol):
    """Protocol for ctypes.Union.

    Represents a C union where fields share memory.

    Example:
        class Value(Union):
            _fields_ = [("i", c_int), ("f", c_float)]
    """

    # Class attributes
    _fields_: list[tuple[str, type] | tuple[str, type, int]]
    """Field definitions: (name, type) or (name, type, bitwidth)."""

    def __getattr__(self, name: str) -> Any:
        """Get field value by name."""
        ...

    @override
    def __setattr__(self, name: str, value: object) -> None:
        """Set field value by name."""
        ...


# ============================================================================
# Array Protocol
# ============================================================================


@runtime_checkable
class ArrayProtocol(CDataProtocol, Protocol, Generic[CT_co]):
    """Protocol for ctypes.Array.

    Fixed-size array of C types.

    Example:
        IntArray10 = c_int * 10
        arr = IntArray10()
    """

    @property
    def _length_(self) -> int:
        """Number of elements in the array."""
        ...

    @property
    def _type_(self) -> type[CT_co]:
        """Type of array elements."""
        ...

    @property
    def raw(self) -> bytes:
        """Raw bytes of the array (for char arrays)."""
        ...

    @property
    def value(self) -> Any:
        """String value (for char arrays)."""
        ...

    def __len__(self) -> int:
        """Return array length."""
        ...

    @overload
    def __getitem__(self, index: int) -> Any:
        """Get element at index."""
        ...

    @overload
    def __getitem__(self, index: slice) -> list[Any]:
        """Get slice of elements."""
        ...

    def __getitem__(self, index: int | slice) -> Any | list[Any]:
        """Get element or slice."""
        ...

    def __setitem__(self, index: int, value: Any) -> None:
        """Set element at index."""
        ...

    def __iter__(self) -> Iterator[Any]:
        """Iterate over elements."""
        ...


# ============================================================================
# Pointer Protocol
# ============================================================================


@runtime_checkable
class PointerProtocol(CDataProtocol, Protocol, Generic[CT]):
    """Protocol for ctypes pointer types.

    Represents a pointer to a C data type.

    Example:
        p = pointer(c_int(42))
        print(p.contents)  # 42
    """

    @property
    def _type_(self) -> type[CT]:
        """Type that this pointer points to."""
        ...

    @property
    def contents(self) -> CT:
        """The object this pointer points to."""
        ...

    @contents.setter
    def contents(self, value: CT) -> None:
        """Set the pointed-to object."""
        ...

    @overload
    def __getitem__(self, index: int) -> Any:
        """Get element at pointer offset."""
        ...

    @overload
    def __getitem__(self, index: slice) -> list[Any]:
        """Get slice from pointer."""
        ...

    def __getitem__(self, index: int | slice) -> Any | list[Any]:
        """Get element or slice."""
        ...

    def __setitem__(self, index: int, value: Any) -> None:
        """Set element at pointer offset."""
        ...


# ============================================================================
# Function Pointer Protocol
# ============================================================================


@runtime_checkable
class CFuncPtrProtocol(CDataProtocol, Protocol):
    """Protocol for C function pointers.

    Represents a pointer to a C function.

    Example:
        CALLBACK = CFUNCTYPE(c_int, c_int, c_int)

        @CALLBACK
        def my_callback(a, b):
            return a + b
    """

    _flags_: int
    """Function calling convention flags."""

    argtypes: list[type] | None
    """Argument types for the function."""

    restype: type | None
    """Return type of the function."""

    errcheck: Any
    """Error checking callback."""

    def __call__(self, *args: Any, **kwargs: Any) -> Any:
        """Call the C function.

        Args:
            *args: Arguments to pass to the function
            **kwargs: Keyword arguments

        Returns:
            Function return value
        """
        ...


# ============================================================================
# CDLL Protocol
# ============================================================================


@runtime_checkable
class CDLLProtocol(Protocol):
    """Protocol for loaded shared libraries.

    Represents a loaded DLL or shared object.

    Example:
        libc = CDLL("libc.so.6")
        libc.printf(b"Hello, %s!\\n", b"World")
    """

    _name: str
    """Name/path of the loaded library."""

    _handle: int
    """OS handle to the loaded library."""

    def __getattr__(self, name: str) -> CFuncPtrProtocol:
        """Get a function from the library by name.

        Args:
            name: Function name

        Returns:
            Callable function object
        """
        ...

    def __getitem__(self, name: str) -> CFuncPtrProtocol:
        """Get a function from the library by name.

        Args:
            name: Function name

        Returns:
            Callable function object
        """
        ...


# ============================================================================
# Helper Functions
# ============================================================================


def addressof(obj: CDataProtocol) -> int:
    """Get the address of a ctypes object.

    Args:
        obj: CData object

    Returns:
        Memory address as integer
    """
    ...


def sizeof(obj: CDataProtocol | type[CDataProtocol]) -> int:
    """Get the size of a ctypes type or object.

    Args:
        obj: CData type or instance

    Returns:
        Size in bytes
    """
    ...


def alignment(obj: CDataProtocol | type[CDataProtocol]) -> int:
    """Get the alignment of a ctypes type or object.

    Args:
        obj: CData type or instance

    Returns:
        Alignment in bytes
    """
    ...


def byref(obj: CDataProtocol, offset: int = 0) -> Any:
    """Get a lightweight pointer to an object.

    More efficient than pointer() for passing by reference.

    Args:
        obj: CData object
        offset: Byte offset

    Returns:
        Light-weight pointer object
    """
    ...


def pointer(obj: CT) -> PointerProtocol[CT]:
    """Create a pointer to a ctypes object.

    Args:
        obj: CData object to point to

    Returns:
        Pointer to the object
    """
    ...


def POINTER(type_: type[CT]) -> type[PointerProtocol[CT]]:
    """Create a pointer type.

    Args:
        type_: Type to create pointer type for

    Returns:
        Pointer type class
    """
    ...


def cast(
    obj: CDataProtocol | int,
    typ: type[CT],
) -> CT:
    """Cast a pointer or address to a different type.

    Args:
        obj: CData pointer or integer address
        typ: Target type

    Returns:
        Cast result
    """
    ...


def LoadLibrary(name: str, mode: int = 0) -> int:
    """Load a shared library (Windows).

    Args:
        name: Library path
        mode: Load mode flags

    Returns:
        Library handle
    """
    ...


def FreeLibrary(handle: int) -> None:
    """Free a loaded library (Windows).

    Args:
        handle: Library handle from LoadLibrary
    """
    ...


def get_errno() -> int:
    """Get the current errno value.

    Returns:
        Current errno value
    """
    ...


def set_errno(value: int) -> int:
    """Set the errno value.

    Args:
        value: New errno value

    Returns:
        Previous errno value
    """
    ...


def get_last_error() -> int:
    """Get Windows last error (Windows only).

    Returns:
        GetLastError() value
    """
    ...


def set_last_error(value: int) -> int:
    """Set Windows last error (Windows only).

    Args:
        value: New error value

    Returns:
        Previous error value
    """
    ...


# ============================================================================
# Constants
# ============================================================================

RTLD_GLOBAL: Final[int] = 0x100
"""Load library symbols into global namespace."""

RTLD_LOCAL: Final[int] = 0
"""Load library symbols locally (default)."""


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Base Protocols
    "CDataProtocol",
    "SimpleCDataProtocol",
    # Structure Protocols
    "StructureProtocol",
    "UnionProtocol",
    # Container Protocols
    "ArrayProtocol",
    "PointerProtocol",
    # Function Protocols
    "CFuncPtrProtocol",
    "CDLLProtocol",
    # Helper Functions
    "addressof",
    "sizeof",
    "alignment",
    "byref",
    "pointer",
    "POINTER",
    "cast",
    "LoadLibrary",
    "FreeLibrary",
    "get_errno",
    "set_errno",
    "get_last_error",
    "set_last_error",
    # Constants
    "RTLD_GLOBAL",
    "RTLD_LOCAL",
]
