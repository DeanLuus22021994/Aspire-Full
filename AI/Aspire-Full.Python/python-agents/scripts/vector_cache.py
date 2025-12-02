"""
Redis and Qdrant integration for efficient vector storage.

Provides a low-footprint, high-performance abstraction for:
- Redis: Caching, pub/sub, and session state
- Qdrant: Vector similarity search with minimal memory usage

Optimized for Python 3.15 free-threading with concurrent operations.
"""

from __future__ import annotations

import importlib
import sys
from dataclasses import dataclass, field
from enum import StrEnum
from pathlib import Path
from typing import TYPE_CHECKING, Any, Final

# Add src to path for vendor imports
_SCRIPT_DIR = Path(__file__).parent
_PROJECT_ROOT = _SCRIPT_DIR.parent
_SRC_DIR = _PROJECT_ROOT / "src"
if str(_SRC_DIR) not in sys.path:
    sys.path.insert(0, str(_SRC_DIR))

if TYPE_CHECKING:
    from collections.abc import Sequence

# ============================================================================
# Constants
# ============================================================================

DEFAULT_REDIS_URL: Final[str] = "redis://localhost:6379/0"
DEFAULT_QDRANT_URL: Final[str] = "http://localhost:6333"
DEFAULT_COLLECTION_NAME: Final[str] = "aspire_vectors"
DEFAULT_VECTOR_SIZE: Final[int] = 384  # all-MiniLM-L6-v2 dimension


# ============================================================================
# Enums
# ============================================================================


class VectorDistanceMetric(StrEnum):
    """Distance metrics for vector similarity."""

    COSINE = "Cosine"
    EUCLIDEAN = "Euclid"
    DOT_PRODUCT = "Dot"


class CacheStrategy(StrEnum):
    """Redis caching strategies."""

    WRITE_THROUGH = "write_through"
    WRITE_BEHIND = "write_behind"
    READ_THROUGH = "read_through"
    CACHE_ASIDE = "cache_aside"


class IndexingMode(StrEnum):
    """Qdrant indexing modes for memory optimization."""

    IN_MEMORY = "in_memory"
    MMAP = "mmap"  # Memory-mapped for low footprint
    ON_DISK = "on_disk"


# ============================================================================
# Configuration
# ============================================================================


@dataclass(frozen=True, slots=True)
class RedisConfig:
    """Redis connection configuration."""

    url: str = DEFAULT_REDIS_URL
    max_connections: int = 10
    socket_timeout: float = 5.0
    socket_connect_timeout: float = 5.0
    decode_responses: bool = True
    cache_strategy: CacheStrategy = CacheStrategy.CACHE_ASIDE


@dataclass(frozen=True, slots=True)
class QdrantConfig:
    """Qdrant connection configuration."""

    url: str = DEFAULT_QDRANT_URL
    collection_name: str = DEFAULT_COLLECTION_NAME
    vector_size: int = DEFAULT_VECTOR_SIZE
    distance_metric: VectorDistanceMetric = VectorDistanceMetric.COSINE
    indexing_mode: IndexingMode = IndexingMode.MMAP
    # Memory optimization settings
    on_disk_payload: bool = True  # Store payloads on disk
    quantization_enabled: bool = True  # Scalar quantization for 4x memory reduction


@dataclass
class VectorSearchResult:
    """Result from vector similarity search."""

    id: str
    score: float
    payload: "dict[str, Any]" = field(default_factory=lambda: {})
    vector: list[float] | None = None


# ============================================================================
# Redis Client Abstraction
# ============================================================================


class RedisClientManager:
    """Manages Redis connections with connection pooling."""

    def __init__(self, config: RedisConfig | None = None) -> None:
        self.config = config or RedisConfig()
        self._client: Any = None
        self._pool: Any = None

    def _get_client(self) -> Any:
        """Get or create Redis client using vendor abstraction."""
        if self._client is not None:
            return self._client

        try:
            from aspire_agents._vendor._redis import from_url

            self._client = from_url(
                self.config.url,
                max_connections=self.config.max_connections,
                socket_timeout=self.config.socket_timeout,
                socket_connect_timeout=self.config.socket_connect_timeout,
                decode_responses=self.config.decode_responses,
            )
            return self._client
        except (ImportError, RuntimeError) as e:
            print(f"Redis not available: {e}")
            return None

    def get(self, key: str) -> str | None:
        """Get value from Redis."""
        client = self._get_client()
        if client is None:
            return None
        return client.get(key)

    def set(
        self, key: str, value: str, ex: int | None = None
    ) -> bool:
        """Set value in Redis with optional expiration."""
        client = self._get_client()
        if client is None:
            return False
        return client.set(key, value, ex=ex)

    def delete(self, key: str) -> int:
        """Delete key from Redis."""
        client = self._get_client()
        if client is None:
            return 0
        return client.delete(key)

    def exists(self, key: str) -> bool:
        """Check if key exists."""
        client = self._get_client()
        if client is None:
            return False
        return client.exists(key) > 0

    def ping(self) -> bool:
        """Check Redis connectivity."""
        client = self._get_client()
        if client is None:
            return False
        try:
            return client.ping()
        except Exception:
            return False


# ============================================================================
# Qdrant Client Abstraction
# ============================================================================


class QdrantClientManager:
    """Manages Qdrant connections with memory-optimized settings."""

    def __init__(self, config: QdrantConfig | None = None) -> None:
        self.config = config or QdrantConfig()
        self._client: Any = None

    def _get_client(self) -> Any:
        """Get or create Qdrant client."""
        if self._client is not None:
            return self._client

        try:
            qdrant_module: Any = importlib.import_module("qdrant_client")
            client_class: Any = getattr(qdrant_module, "QdrantClient")
            self._client = client_class(url=self.config.url)
            return self._client
        except ImportError:
            print("qdrant-client not installed. Install with: pip install qdrant-client")
            return None

    def ensure_collection(self) -> bool:
        """Ensure collection exists with optimized settings."""
        client = self._get_client()
        if client is None:
            return False

        try:
            qdrant_models: Any = importlib.import_module("qdrant_client.models")

            # Check if collection exists
            collections = client.get_collections().collections
            collection_names = [c.name for c in collections]

            if self.config.collection_name in collection_names:
                return True

            # Create collection with memory-optimized settings
            vector_config = qdrant_models.VectorParams(
                size=self.config.vector_size,
                distance=getattr(qdrant_models.Distance, self.config.distance_metric),
                on_disk=self.config.indexing_mode == IndexingMode.ON_DISK,
            )

            # Optimizers for low memory footprint
            optimizers_config = qdrant_models.OptimizersConfigDiff(
                memmap_threshold=20000,  # Use mmap for segments > 20k vectors
                indexing_threshold=20000,
            )

            # Quantization for 4x memory reduction
            quantization_config = None
            if self.config.quantization_enabled:
                quantization_config = qdrant_models.ScalarQuantization(
                    scalar=qdrant_models.ScalarQuantizationConfig(
                        type=qdrant_models.ScalarType.INT8,
                        always_ram=False,  # Keep quantized on disk
                    )
                )

            client.create_collection(
                collection_name=self.config.collection_name,
                vectors_config=vector_config,
                optimizers_config=optimizers_config,
                quantization_config=quantization_config,
                on_disk_payload=self.config.on_disk_payload,
            )

            print(f"Created collection: {self.config.collection_name}")
            return True

        except Exception as e:
            print(f"Failed to ensure collection: {e}")
            return False

    def upsert_vectors(
        self,
        ids: "Sequence[str]",
        vectors: "Sequence[Sequence[float]]",
        payloads: "Sequence[dict[str, Any]] | None" = None,
    ) -> bool:
        """Upsert vectors with optional payloads."""
        client = self._get_client()
        if client is None:
            return False

        try:
            qdrant_models: Any = importlib.import_module("qdrant_client.models")

            points: list[Any] = []
            for i, (point_id, vector) in enumerate(zip(ids, vectors, strict=False)):
                payload = payloads[i] if payloads else {}
                points.append(
                    qdrant_models.PointStruct(
                        id=point_id,
                        vector=list(vector),
                        payload=payload,
                    )
                )

            client.upsert(
                collection_name=self.config.collection_name,
                points=points,
            )
            return True

        except Exception as e:
            print(f"Failed to upsert vectors: {e}")
            return False

    def search(
        self,
        query_vector: "Sequence[float]",
        limit: int = 10,
        score_threshold: float | None = None,
    ) -> list[VectorSearchResult]:
        """Search for similar vectors."""
        client = self._get_client()
        if client is None:
            return []

        try:
            results = client.search(
                collection_name=self.config.collection_name,
                query_vector=list(query_vector),
                limit=limit,
                score_threshold=score_threshold,
            )

            return [
                VectorSearchResult(
                    id=str(r.id),
                    score=r.score,
                    payload=r.payload or {},
                    vector=r.vector,
                )
                for r in results
            ]

        except Exception as e:
            print(f"Search failed: {e}")
            return []

    def delete_vectors(self, ids: "Sequence[str]") -> bool:
        """Delete vectors by ID."""
        client = self._get_client()
        if client is None:
            return False

        try:
            qdrant_models: Any = importlib.import_module("qdrant_client.models")

            client.delete(
                collection_name=self.config.collection_name,
                points_selector=qdrant_models.PointIdsList(points=list(ids)),
            )
            return True

        except Exception as e:
            print(f"Delete failed: {e}")
            return False

    def get_collection_info(self) -> dict[str, Any] | None:
        """Get collection statistics for monitoring."""
        client = self._get_client()
        if client is None:
            return None

        try:
            info = client.get_collection(self.config.collection_name)
            return {
                "name": info.config.params.vectors.size if hasattr(info.config.params, 'vectors') else 0,
                "points_count": info.points_count,
                "indexed_vectors_count": info.indexed_vectors_count,
                "status": str(info.status),
                "optimizer_status": str(info.optimizer_status),
            }
        except Exception:
            return None


# ============================================================================
# Unified Vector Cache
# ============================================================================


class VectorCache:
    """
    Unified cache combining Redis (metadata) and Qdrant (vectors).

    Low memory footprint architecture:
    - Redis: Stores metadata, embeddings cache keys
    - Qdrant: Stores vectors with mmap/on-disk indexing

    Benefits:
    - Vectors stored on disk with mmap access (low RAM)
    - Metadata in Redis for fast lookups
    - Quantization reduces vector memory 4x
    """

    def __init__(
        self,
        redis_config: RedisConfig | None = None,
        qdrant_config: QdrantConfig | None = None,
    ) -> None:
        self.redis = RedisClientManager(redis_config)
        self.qdrant = QdrantClientManager(qdrant_config)

    def initialize(self) -> bool:
        """Initialize both backends."""
        redis_ok = self.redis.ping()
        qdrant_ok = self.qdrant.ensure_collection()

        if redis_ok:
            print("✓ Redis connected")
        else:
            print("✗ Redis not available")

        if qdrant_ok:
            print("✓ Qdrant collection ready")
        else:
            print("✗ Qdrant not available")

        return redis_ok and qdrant_ok

    def store(
        self,
        key: str,
        vector: "Sequence[float]",
        metadata: dict[str, Any] | None = None,
        ttl: int | None = None,
    ) -> bool:
        """Store vector with metadata."""
        # Store metadata in Redis
        import json
        meta_key = f"vec:meta:{key}"
        meta_value = json.dumps(metadata or {})
        self.redis.set(meta_key, meta_value, ex=ttl)

        # Store vector in Qdrant
        payload = metadata.copy() if metadata else {}
        payload["_key"] = key
        return self.qdrant.upsert_vectors([key], [list(vector)], [payload])

    def search_similar(
        self,
        query_vector: "Sequence[float]",
        limit: int = 10,
    ) -> list[VectorSearchResult]:
        """Search for similar vectors."""
        return self.qdrant.search(query_vector, limit=limit)

    def get_metadata(self, key: str) -> dict[str, Any] | None:
        """Get metadata from Redis."""
        import json
        meta_key = f"vec:meta:{key}"
        value = self.redis.get(meta_key)
        if value:
            return json.loads(value)
        return None

    def delete(self, key: str) -> bool:
        """Delete from both backends."""
        self.redis.delete(f"vec:meta:{key}")
        return self.qdrant.delete_vectors([key])

    def stats(self) -> dict[str, Any]:
        """Get cache statistics."""
        qdrant_info = self.qdrant.get_collection_info()
        return {
            "redis_connected": self.redis.ping(),
            "qdrant_collection": qdrant_info,
        }


# ============================================================================
# Main Entry Point
# ============================================================================


def main() -> int:
    """Test vector cache connectivity."""
    print("=== Vector Cache Status ===\n")

    cache = VectorCache()

    if cache.initialize():
        print("\nVector cache initialized successfully!")
        stats = cache.stats()
        print(f"\nStats: {stats}")
        return 0
    else:
        print("\nVector cache initialization failed.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
