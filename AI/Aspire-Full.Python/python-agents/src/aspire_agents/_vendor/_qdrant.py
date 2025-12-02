"""
Qdrant vendor abstraction for type-safe vector database operations.

Provides protocol definitions and factory functions for Qdrant client
without hard dependencies, enabling lazy loading and proper typing.

Memory Optimization:
- MMAP indexing: Vectors stored on disk, accessed via memory mapping
- Scalar quantization: 4x memory reduction (float32 → int8)
- On-disk payloads: Metadata stored on disk, not RAM

Python 3.15 Free-Threading:
- Thread-safe client instances
- No GIL contention for IO-bound vector operations
"""

from __future__ import annotations

import importlib
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any, Final, Protocol, Sequence, runtime_checkable

# Import centralized enums from _enums.py
from ._enums import (
    QdrantDistance as Distance,
)
from ._enums import (
    QdrantIndexType as IndexType,
)
from ._enums import (
    QdrantPayloadType as PayloadSchemaType,
)
from ._enums import (
    QdrantQuantization as QuantizationType,
)

if TYPE_CHECKING:
    pass  # No type-only imports needed

# ============================================================================
# Constants
# ============================================================================

QDRANT_DEFAULT_URL: Final[str] = "http://localhost:6333"
QDRANT_DEFAULT_GRPC_PORT: Final[int] = 6334
QDRANT_DEFAULT_TIMEOUT: Final[float] = 30.0


# ============================================================================
# Data Classes
# ============================================================================


@dataclass(frozen=True, slots=True)
class VectorParams:
    """Vector configuration for a collection."""

    size: int
    distance: Distance = Distance.COSINE
    on_disk: bool = False
    hnsw_config: HnswConfig | None = None
    quantization_config: QuantizationConfig | None = None


@dataclass(frozen=True, slots=True)
class HnswConfig:
    """HNSW algorithm configuration."""

    m: int = 16  # Number of edges per node
    ef_construct: int = 100  # Construction time/accuracy trade-off
    full_scan_threshold: int = 10000  # Use brute force below this
    max_indexing_threads: int = 0  # 0 = auto-detect
    on_disk: bool = False  # Store HNSW graph on disk


@dataclass(frozen=True, slots=True)
class QuantizationConfig:
    """Quantization configuration for memory optimization."""

    quantization_type: QuantizationType = QuantizationType.SCALAR_INT8
    always_ram: bool = False  # Keep quantized vectors in RAM
    rescore: bool = True  # Rescore with original vectors


@dataclass(frozen=True, slots=True)
class OptimizersConfig:
    """Collection optimizer configuration."""

    memmap_threshold: int = 20000  # Vectors count before using mmap
    indexing_threshold: int = 20000  # Vectors count before indexing
    flush_interval_sec: int = 5
    max_optimization_threads: int = 0  # 0 = auto-detect


@dataclass(slots=True)
class PointStruct:
    """A point with ID, vector, and optional payload."""

    id: str | int
    vector: list[float]
    payload: "dict[str, Any]" = field(default_factory=lambda: {})


@dataclass(frozen=True, slots=True)
class ScoredPoint:
    """Search result with similarity score."""

    id: str | int
    score: float
    payload: "dict[str, Any]" = field(default_factory=lambda: {})
    vector: list[float] | None = None


@dataclass(frozen=True, slots=True)
class CollectionInfo:
    """Collection statistics and configuration."""

    name: str
    vectors_count: int
    points_count: int
    indexed_vectors_count: int
    status: str
    optimizer_status: str
    disk_data_size: int = 0
    ram_data_size: int = 0


@dataclass(frozen=True, slots=True)
class Filter:
    """Filter for search queries."""

    must: "list[FieldCondition]"
    should: "list[FieldCondition]"
    must_not: "list[FieldCondition]"


@dataclass(frozen=True, slots=True)
class FieldCondition:
    """Condition for filtering by payload field."""

    key: str
    match: Any = None  # Exact match
    range_: tuple[Any, Any] | None = None  # Range query (min, max)


# ============================================================================
# Protocols
# ============================================================================


@runtime_checkable
class QdrantClientProtocol(Protocol):
    """Protocol for Qdrant client operations."""

    def get_collections(self) -> CollectionsResponse:
        """List all collections."""
        ...

    def create_collection(
        self,
        collection_name: str,
        vectors_config: VectorParams,
        *,
        optimizers_config: OptimizersConfig | None = None,
        on_disk_payload: bool = False,
    ) -> bool:
        """Create a new collection."""
        ...

    def delete_collection(self, collection_name: str) -> bool:
        """Delete a collection."""
        ...

    def get_collection(self, collection_name: str) -> CollectionInfo:
        """Get collection info."""
        ...

    def upsert(
        self,
        collection_name: str,
        points: "Sequence[PointStruct]",
        *,
        wait: bool = True,
    ) -> UpdateResult:
        """Upsert points into collection."""
        ...

    def search(
        self,
        collection_name: str,
        query_vector: "Sequence[float]",
        *,
        limit: int = 10,
        query_filter: Filter | None = None,
        with_payload: bool = True,
        with_vectors: bool = False,
        score_threshold: float | None = None,
    ) -> list[ScoredPoint]:
        """Search for similar vectors."""
        ...

    def delete(
        self,
        collection_name: str,
        points_selector: PointsSelector,
        *,
        wait: bool = True,
    ) -> UpdateResult:
        """Delete points from collection."""
        ...

    def retrieve(
        self,
        collection_name: str,
        ids: "Sequence[str | int]",
        *,
        with_payload: bool = True,
        with_vectors: bool = False,
    ) -> list[PointStruct]:
        """Retrieve points by ID."""
        ...


@runtime_checkable
class AsyncQdrantClientProtocol(Protocol):
    """Protocol for async Qdrant client operations."""

    async def get_collections(self) -> CollectionsResponse:
        """List all collections."""
        ...

    async def create_collection(
        self,
        collection_name: str,
        vectors_config: VectorParams,
        *,
        optimizers_config: OptimizersConfig | None = None,
        on_disk_payload: bool = False,
    ) -> bool:
        """Create a new collection."""
        ...

    async def upsert(
        self,
        collection_name: str,
        points: "Sequence[PointStruct]",
        *,
        wait: bool = True,
    ) -> UpdateResult:
        """Upsert points into collection."""
        ...

    async def search(
        self,
        collection_name: str,
        query_vector: "Sequence[float]",
        *,
        limit: int = 10,
        query_filter: Filter | None = None,
        with_payload: bool = True,
        with_vectors: bool = False,
        score_threshold: float | None = None,
    ) -> list[ScoredPoint]:
        """Search for similar vectors."""
        ...


# ============================================================================
# Response Types
# ============================================================================


@dataclass(frozen=True, slots=True)
class CollectionsResponse:
    """Response from get_collections."""

    collections: "list[CollectionDescription]" = field(default_factory=lambda: [])


@dataclass(frozen=True, slots=True)
class CollectionDescription:
    """Brief collection description."""

    name: str


@dataclass(frozen=True, slots=True)
class UpdateResult:
    """Result of update operation."""

    operation_id: int | None = None
    status: str = "completed"


@dataclass(frozen=True, slots=True)
class PointsSelector:
    """Selector for points to delete."""

    points: "list[str | int]" = field(default_factory=lambda: [])


# ============================================================================
# Factory Functions
# ============================================================================


def create_client(
    url: str = QDRANT_DEFAULT_URL,
    *,
    prefer_grpc: bool = False,
    grpc_port: int = QDRANT_DEFAULT_GRPC_PORT,
    timeout: float = QDRANT_DEFAULT_TIMEOUT,
) -> QdrantClientProtocol:
    """
    Create a Qdrant client instance.

    Args:
        url: Qdrant server URL
        prefer_grpc: Use gRPC instead of HTTP (faster for large batches)
        grpc_port: gRPC port (default 6334)
        timeout: Request timeout in seconds

    Returns:
        Qdrant client instance

    Raises:
        ImportError: If qdrant-client is not installed
    """
    qdrant_module: Any = importlib.import_module("qdrant_client")
    client_class: Any = getattr(qdrant_module, "QdrantClient")

    return client_class(
        url=url,
        prefer_grpc=prefer_grpc,
        grpc_port=grpc_port,
        timeout=timeout,
    )


def create_async_client(
    url: str = QDRANT_DEFAULT_URL,
    *,
    prefer_grpc: bool = False,
    grpc_port: int = QDRANT_DEFAULT_GRPC_PORT,
    timeout: float = QDRANT_DEFAULT_TIMEOUT,
) -> AsyncQdrantClientProtocol:
    """
    Create an async Qdrant client instance.

    Args:
        url: Qdrant server URL
        prefer_grpc: Use gRPC instead of HTTP
        grpc_port: gRPC port (default 6334)
        timeout: Request timeout in seconds

    Returns:
        Async Qdrant client instance

    Raises:
        ImportError: If qdrant-client is not installed
    """
    qdrant_module: Any = importlib.import_module("qdrant_client")
    async_client_class: Any = getattr(qdrant_module, "AsyncQdrantClient")

    return async_client_class(
        url=url,
        prefer_grpc=prefer_grpc,
        grpc_port=grpc_port,
        timeout=timeout,
    )


def create_local_client(
    path: str,
    *,
    force_disable_check_same_thread: bool = True,
) -> QdrantClientProtocol:
    """
    Create a local Qdrant client using embedded SQLite storage.

    This is useful for development and testing without running
    a Qdrant server.

    Args:
        path: Path to store the database files
        force_disable_check_same_thread: Allow multi-threaded access

    Returns:
        Local Qdrant client instance
    """
    qdrant_module: Any = importlib.import_module("qdrant_client")
    client_class: Any = getattr(qdrant_module, "QdrantClient")

    return client_class(
        path=path,
        force_disable_check_same_thread=force_disable_check_same_thread,
    )


# ============================================================================
# Exports
# ============================================================================

__all__ = [
    # Constants
    "QDRANT_DEFAULT_URL",
    "QDRANT_DEFAULT_GRPC_PORT",
    "QDRANT_DEFAULT_TIMEOUT",
    # Enums
    "Distance",
    "IndexType",
    "QuantizationType",
    "PayloadSchemaType",
    # Data Classes
    "VectorParams",
    "HnswConfig",
    "QuantizationConfig",
    "OptimizersConfig",
    "PointStruct",
    "ScoredPoint",
    "CollectionInfo",
    "Filter",
    "FieldCondition",
    # Response Types
    "CollectionsResponse",
    "CollectionDescription",
    "UpdateResult",
    "PointsSelector",
    # Protocols
    "QdrantClientProtocol",
    "AsyncQdrantClientProtocol",
    # Factory Functions
    "create_client",
    "create_async_client",
    "create_local_client",
]
