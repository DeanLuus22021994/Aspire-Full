"""Centralized Enum Definitions for Vendor Abstractions.

This module contains all enumerations used across the vendor abstraction layer,
organized by their corresponding interface category in the VendorFactory.

Categories align with VendorFactory interface collections:
- VendorCategory: Factory-level categorization
- Torch: TorchInterfaces enums
- Transformers: TransformersInterfaces enums
- Agents: AgentsInterfaces enums
- OpenAI: OpenAIInterfaces enums
- DockerModelRunner: DockerModelRunnerInterfaces enums
- Redis: RedisInterfaces enums
- Threading: ThreadingInterfaces enums
- CTypes: CTypesInterfaces enums
- Profiler: ProfilerInterfaces enums
- Safetensors: SafetensorsInterfaces enums
- Pytest: PytestInterfaces enums

All string literals are centralized here to avoid hardcoding across the codebase.
"""

from __future__ import annotations

from enum import Enum, StrEnum, auto
from typing import Final

# ============================================================================
# Factory Category Enum
# ============================================================================


class VendorCategory(Enum):
    """Categories of vendor abstractions.

    Used by VendorFactory and ProtocolRegistry for categorizing
    protocol implementations.
    """

    # Core ML/AI
    TORCH = auto()
    """PyTorch tensor operations."""

    TRANSFORMERS = auto()
    """HuggingFace Transformers models."""

    SAFETENSORS = auto()
    """Safe tensor serialization."""

    # Agent Frameworks
    AGENTS = auto()
    """OpenAI Agents SDK."""

    OPENAI = auto()
    """OpenAI API client."""

    DOCKER_MODEL_RUNNER = auto()
    """Docker Model Runner for local LLM inference."""

    # Infrastructure
    REDIS = auto()
    """Redis database client."""

    THREADING = auto()
    """Python 3.15 free-threading primitives."""

    CTYPES = auto()
    """C FFI interface."""

    # Development
    PROFILER = auto()
    """Python profiling (cProfile/pstats)."""

    PYTEST = auto()
    """Testing framework."""


# ============================================================================
# Torch Enums
# ============================================================================


class TorchDeviceType(StrEnum):
    """Device types for PyTorch tensors.

    Corresponds to TorchInterfaces.
    Specifies where tensors are stored and computed.
    """

    CPU = "cpu"
    """CPU device (default)."""

    CUDA = "cuda"
    """NVIDIA CUDA GPU device."""

    MPS = "mps"
    """Apple Metal Performance Shaders (M1/M2/M3)."""

    XPU = "xpu"
    """Intel XPU device."""

    NPU = "npu"
    """Neural Processing Unit."""

    @classmethod
    def from_string(cls, value: str) -> "TorchDeviceType":
        """Create from string value.

        Args:
            value: Device type string (case-insensitive).

        Returns:
            Corresponding TorchDeviceType.

        Raises:
            ValueError: If value is not a valid device type.
        """
        # Handle device:index format (e.g., "cuda:0")
        base = value.split(":")[0].lower()
        try:
            return cls(base)
        except ValueError as e:
            valid = ", ".join(m.value for m in cls)
            msg = f"Invalid device type '{value}'. Valid: {valid}"
            raise ValueError(msg) from e

    @property
    def supports_gpu(self) -> bool:
        """Check if this device type supports GPU acceleration.

        Returns:
            True for CUDA, MPS, XPU, NPU.
        """
        return self in (
            TorchDeviceType.CUDA,
            TorchDeviceType.MPS,
            TorchDeviceType.XPU,
            TorchDeviceType.NPU,
        )


class TorchDtypeEnum(StrEnum):
    """Data types for PyTorch tensors.

    Corresponds to TorchInterfaces.
    Specifies the numeric type of tensor elements.
    """

    # Floating point
    FLOAT16 = "float16"
    """16-bit floating point (half precision)."""

    FLOAT32 = "float32"
    """32-bit floating point (single precision)."""

    FLOAT64 = "float64"
    """64-bit floating point (double precision)."""

    BFLOAT16 = "bfloat16"
    """Brain floating point (16-bit, better range than float16)."""

    # Integer
    INT8 = "int8"
    """8-bit signed integer."""

    INT16 = "int16"
    """16-bit signed integer."""

    INT32 = "int32"
    """32-bit signed integer."""

    INT64 = "int64"
    """64-bit signed integer."""

    UINT8 = "uint8"
    """8-bit unsigned integer."""

    # Boolean
    BOOL = "bool"
    """Boolean type."""

    # Complex
    COMPLEX64 = "complex64"
    """Complex number with 32-bit float components."""

    COMPLEX128 = "complex128"
    """Complex number with 64-bit float components."""

    @property
    def is_floating_point(self) -> bool:
        """Check if this is a floating point type.

        Returns:
            True for float16, float32, float64, bfloat16.
        """
        return self in (
            TorchDtypeEnum.FLOAT16,
            TorchDtypeEnum.FLOAT32,
            TorchDtypeEnum.FLOAT64,
            TorchDtypeEnum.BFLOAT16,
        )

    @property
    def is_integer(self) -> bool:
        """Check if this is an integer type.

        Returns:
            True for int8, int16, int32, int64, uint8.
        """
        return self in (
            TorchDtypeEnum.INT8,
            TorchDtypeEnum.INT16,
            TorchDtypeEnum.INT32,
            TorchDtypeEnum.INT64,
            TorchDtypeEnum.UINT8,
        )

    @property
    def is_complex(self) -> bool:
        """Check if this is a complex type.

        Returns:
            True for complex64, complex128.
        """
        return self in (TorchDtypeEnum.COMPLEX64, TorchDtypeEnum.COMPLEX128)


# Backwards compatibility alias
DeviceType = TorchDeviceType


# ============================================================================
# Docker Model Runner Enums
# ============================================================================


class GpuBackendType(Enum):
    """GPU backend enumeration for Docker Model Runner.

    Corresponds to DockerModelRunnerInterfaces.
    Used for type-safe GPU backend selection.
    """

    CUDA = auto()
    """NVIDIA CUDA backend (most common for NVIDIA GPUs)."""

    ROCM = auto()
    """AMD ROCm backend (for AMD GPUs)."""

    MUSA = auto()
    """Moore Threads MUSA backend."""

    CANN = auto()
    """Huawei CANN backend."""

    AUTO = auto()
    """Automatic detection (may not work correctly)."""

    NONE = auto()
    """CPU only, no GPU acceleration."""

    def to_cli_arg(self) -> str:
        """Convert to CLI argument string.

        Returns:
            Lowercase backend name for docker model CLI.
        """
        return self.name.lower()

    @classmethod
    def from_string(cls, value: str) -> "GpuBackendType":
        """Create from string value.

        Args:
            value: Backend name (case-insensitive).

        Returns:
            Corresponding GpuBackendType.

        Raises:
            ValueError: If value is not a valid backend.
        """
        try:
            return cls[value.upper()]
        except KeyError as e:
            valid = ", ".join(m.name.lower() for m in cls)
            msg = f"Invalid GPU backend '{value}'. Valid: {valid}"
            raise ValueError(msg) from e


# ============================================================================
# Profiler Enums
# ============================================================================


class ProfilerSortKey(StrEnum):
    """Enumeration of profile statistics sort keys.

    Corresponds to ProfilerInterfaces.
    Used with Stats.sort_stats() to order profiling results.
    """

    CALLS = "calls"
    """Sort by call count."""

    CUMULATIVE = "cumulative"
    """Sort by cumulative time in function and callees."""

    FILENAME = "filename"
    """Sort by file name."""

    LINE = "line"
    """Sort by line number."""

    NAME = "name"
    """Sort by function name."""

    NFL = "nfl"
    """Sort by name/file/line."""

    PCALLS = "pcalls"
    """Sort by primitive call count."""

    STDNAME = "stdname"
    """Sort by standard name (file:line(func))."""

    TIME = "time"
    """Sort by internal time (excluding callees)."""


# Alias for backwards compatibility
SortKey = ProfilerSortKey


# ============================================================================
# Pytest Enums
# ============================================================================


class PytestExitCode(Enum):
    """Pytest exit codes enumeration.

    Corresponds to PytestInterfaces.
    Represents all possible pytest exit codes.
    """

    OK = 0
    """All tests passed."""

    TESTS_FAILED = 1
    """Some tests failed."""

    INTERRUPTED = 2
    """Test run was interrupted (e.g., Ctrl+C)."""

    INTERNAL_ERROR = 3
    """Internal error occurred."""

    USAGE_ERROR = 4
    """Incorrect pytest usage."""

    NO_TESTS_COLLECTED = 5
    """No tests were collected."""

    @property
    def is_success(self) -> bool:
        """Check if this exit code indicates success.

        Returns:
            True only for OK exit code.
        """
        return self == PytestExitCode.OK

    @property
    def is_test_failure(self) -> bool:
        """Check if tests ran but failed.

        Returns:
            True for TESTS_FAILED, XFAILED outcomes.
        """
        return self == PytestExitCode.TESTS_FAILED


# Alias for backwards compatibility
ExitCodeEnum = PytestExitCode


class PytestOutcome(Enum):
    """Test execution outcomes.

    Corresponds to PytestInterfaces.
    Represents individual test result states.
    """

    PASSED = auto()
    """Test passed successfully."""

    FAILED = auto()
    """Test failed with assertion error."""

    SKIPPED = auto()
    """Test was skipped (pytest.skip())."""

    XFAILED = auto()
    """Test was expected to fail and did (pytest.xfail())."""

    XPASSED = auto()
    """Test was expected to fail but passed unexpectedly."""

    ERROR = auto()
    """Error occurred during setup/teardown."""

    @property
    def is_success(self) -> bool:
        """Check if outcome represents a successful test.

        Returns:
            True for PASSED and XFAILED outcomes.
        """
        return self in (PytestOutcome.PASSED, PytestOutcome.XFAILED)

    @property
    def is_failure(self) -> bool:
        """Check if outcome represents a test failure.

        Returns:
            True for FAILED and XPASSED outcomes.
        """
        return self in (PytestOutcome.FAILED, PytestOutcome.XPASSED)


# Alias for backwards compatibility
TestOutcome = PytestOutcome


# ============================================================================
# OpenAI Enums
# ============================================================================


class OpenAIRole(StrEnum):
    """Chat message roles for OpenAI API.

    Corresponds to OpenAIInterfaces.
    Used in ChatMessage role field.
    """

    SYSTEM = "system"
    """System message for setting assistant behavior."""

    USER = "user"
    """User message input."""

    ASSISTANT = "assistant"
    """Assistant response message."""

    TOOL = "tool"
    """Tool/function response message."""

    FUNCTION = "function"
    """Function response (deprecated, use TOOL)."""


class OpenAIFinishReason(StrEnum):
    """Completion finish reasons.

    Corresponds to OpenAIInterfaces.
    Indicates why the model stopped generating.
    """

    STOP = "stop"
    """Model reached natural stopping point or stop sequence."""

    LENGTH = "length"
    """Maximum token limit reached."""

    TOOL_CALLS = "tool_calls"
    """Model wants to call tools."""

    CONTENT_FILTER = "content_filter"
    """Content was filtered by safety systems."""

    FUNCTION_CALL = "function_call"
    """Model wants to call a function (deprecated)."""


# ============================================================================
# Agents Enums
# ============================================================================


class AgentState(StrEnum):
    """Agent execution states.

    Corresponds to AgentsInterfaces.
    Represents agent lifecycle states.
    """

    IDLE = "idle"
    """Agent is not running."""

    RUNNING = "running"
    """Agent is executing."""

    WAITING = "waiting"
    """Agent is waiting for input/tool response."""

    COMPLETED = "completed"
    """Agent execution completed successfully."""

    FAILED = "failed"
    """Agent execution failed with error."""

    CANCELLED = "cancelled"
    """Agent execution was cancelled."""


class ToolType(StrEnum):
    """Agent tool types.

    Corresponds to AgentsInterfaces.
    Classifies different kinds of agent tools.
    """

    FUNCTION = "function"
    """Standard function tool."""

    CODE_INTERPRETER = "code_interpreter"
    """Code execution tool."""

    FILE_SEARCH = "file_search"
    """File search/retrieval tool."""

    HANDOFF = "handoff"
    """Agent handoff tool."""

    HOSTED = "hosted"
    """Hosted tool service."""


# ============================================================================
# Threading Enums
# ============================================================================


class ThreadState(StrEnum):
    """Thread execution states.

    Corresponds to ThreadingInterfaces.
    Represents thread lifecycle states.
    """

    CREATED = "created"
    """Thread object created but not started."""

    RUNNING = "running"
    """Thread is executing."""

    WAITING = "waiting"
    """Thread is waiting on lock/condition."""

    BLOCKED = "blocked"
    """Thread is blocked on I/O or syscall."""

    TERMINATED = "terminated"
    """Thread has finished execution."""


class LockState(StrEnum):
    """Lock states for debugging.

    Corresponds to ThreadingInterfaces.
    Represents lock acquisition states.
    """

    UNLOCKED = "unlocked"
    """Lock is not held by any thread."""

    LOCKED = "locked"
    """Lock is held by a thread."""

    WAITING = "waiting"
    """Threads are waiting for this lock."""


# ============================================================================
# Redis Enums
# ============================================================================


class RedisDataType(StrEnum):
    """Redis data types.

    Corresponds to RedisInterfaces.
    Represents Redis key value types.
    """

    STRING = "string"
    """Simple string value."""

    LIST = "list"
    """Ordered list of strings."""

    SET = "set"
    """Unordered set of unique strings."""

    ZSET = "zset"
    """Sorted set with scores."""

    HASH = "hash"
    """Hash map of field-value pairs."""

    STREAM = "stream"
    """Append-only log of entries."""

    NONE = "none"
    """Key does not exist."""


class RedisPubSubType(StrEnum):
    """Redis Pub/Sub message types.

    Corresponds to RedisInterfaces.
    Represents subscription message types.
    """

    SUBSCRIBE = "subscribe"
    """Subscription confirmation."""

    UNSUBSCRIBE = "unsubscribe"
    """Unsubscription confirmation."""

    MESSAGE = "message"
    """Regular message."""

    PMESSAGE = "pmessage"
    """Pattern-matched message."""

    PSUBSCRIBE = "psubscribe"
    """Pattern subscription confirmation."""

    PUNSUBSCRIBE = "punsubscribe"
    """Pattern unsubscription confirmation."""


# ============================================================================
# Safetensors Enums
# ============================================================================


class SafetensorsFramework(StrEnum):
    """Supported frameworks for safetensors.

    Corresponds to SafetensorsInterfaces.
    Target framework for tensor loading.
    """

    PYTORCH = "pt"
    """PyTorch tensors."""

    TENSORFLOW = "tf"
    """TensorFlow tensors."""

    NUMPY = "np"
    """NumPy arrays."""

    JAX = "jax"
    """JAX arrays."""

    PADDLE = "paddle"
    """PaddlePaddle tensors."""

    MLX = "mlx"
    """MLX arrays (Apple Silicon)."""


# ============================================================================
# CTypes Enums
# ============================================================================


class CTypesCallConv(StrEnum):
    """C calling conventions.

    Corresponds to CTypesInterfaces.
    Specifies how function arguments are passed.
    """

    CDECL = "cdecl"
    """C declaration calling convention (caller cleans stack)."""

    STDCALL = "stdcall"
    """Standard call convention (callee cleans stack, Windows)."""

    THISCALL = "thiscall"
    """C++ member function calling convention."""


class CTypesEndian(StrEnum):
    """Byte ordering for structures.

    Corresponds to CTypesInterfaces.
    Specifies multi-byte value ordering.
    """

    NATIVE = "native"
    """Native byte order of the system."""

    LITTLE = "little"
    """Little-endian byte order."""

    BIG = "big"
    """Big-endian byte order."""


# ============================================================================
# Qdrant Enums
# ============================================================================


class QdrantDistance(StrEnum):
    """Distance metrics for vector similarity.

    Corresponds to QdrantInterfaces.
    Used to measure similarity between vectors.
    """

    COSINE = "Cosine"
    """Cosine similarity (normalized dot product)."""

    EUCLID = "Euclid"
    """Euclidean distance (L2 norm)."""

    DOT = "Dot"
    """Dot product similarity."""

    MANHATTAN = "Manhattan"
    """Manhattan distance (L1 norm)."""


class QdrantIndexType(StrEnum):
    """HNSW index configuration types.

    Corresponds to QdrantInterfaces.
    Controls how vectors are indexed for search.
    """

    DEFAULT = "default"
    """HNSW index (approximate, fast)."""

    FLAT = "flat"
    """Brute force, exact but slow."""


class QdrantQuantization(StrEnum):
    """Quantization methods for memory optimization.

    Corresponds to QdrantInterfaces.
    Reduces memory footprint at cost of precision.
    """

    NONE = "none"
    """No quantization (full precision)."""

    SCALAR_INT8 = "scalar_int8"
    """Scalar quantization to int8 (4x compression)."""

    PRODUCT = "product"
    """Product quantization (higher compression)."""

    BINARY = "binary"
    """Binary quantization (32x compression)."""


class QdrantPayloadType(StrEnum):
    """Payload field types for indexing.

    Corresponds to QdrantInterfaces.
    Specifies how payload fields are indexed.
    """

    KEYWORD = "keyword"
    """Exact match keyword field."""

    INTEGER = "integer"
    """Integer field with range queries."""

    FLOAT = "float"
    """Float field with range queries."""

    GEO = "geo"
    """Geographic coordinates field."""

    TEXT = "text"
    """Full-text search field."""

    BOOL = "bool"
    """Boolean field."""

    DATETIME = "datetime"
    """Date/time field."""


class QdrantStorageMode(StrEnum):
    """Storage modes for memory optimization.

    Corresponds to QdrantInterfaces.
    Controls where vectors and payloads are stored.
    """

    IN_MEMORY = "in_memory"
    """Store everything in RAM (fastest, highest memory)."""

    MMAP = "mmap"
    """Memory-mapped storage (low RAM, moderate speed)."""

    ON_DISK = "on_disk"
    """On-disk storage (lowest RAM, slowest)."""


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Factory
    "VendorCategory",
    # Torch
    "TorchDeviceType",
    "DeviceType",  # Alias
    "TorchDtypeEnum",
    # Docker Model Runner
    "GpuBackendType",
    # Profiler
    "ProfilerSortKey",
    "SortKey",  # Alias
    # Pytest
    "PytestExitCode",
    "ExitCodeEnum",  # Alias
    "PytestOutcome",
    "TestOutcome",  # Alias
    # OpenAI
    "OpenAIRole",
    "OpenAIFinishReason",
    # Agents
    "AgentState",
    "ToolType",
    # Threading
    "ThreadState",
    "LockState",
    # Redis
    "RedisDataType",
    "RedisPubSubType",
    # Safetensors
    "SafetensorsFramework",
    # CTypes
    "CTypesCallConv",
    "CTypesEndian",
    # Qdrant
    "QdrantDistance",
    "QdrantIndexType",
    "QdrantQuantization",
    "QdrantPayloadType",
    "QdrantStorageMode",
]
