# Redis + Qdrant Vector Cache Integration

## Architecture Overview

This integration provides a **low-memory footprint** vector database solution by combining:

- **Redis**: Fast metadata caching, session state, pub/sub
- **Qdrant**: Vector similarity search with memory-optimized storage

## Memory Optimization Strategies

### Qdrant Optimizations

1. **MMAP Indexing** (`IndexingMode.MMAP`)
   - Vectors stored on disk, accessed via memory mapping
   - Only frequently accessed segments kept in RAM
   - Configured via `memmap_threshold` in OptimizersConfig

2. **Scalar Quantization** (`QuantizationType.SCALAR_INT8`)
   - Compresses float32 vectors to int8
   - 4x memory reduction
   - Slight precision loss, rescoring with original vectors

3. **On-Disk Payloads** (`on_disk_payload=True`)
   - Metadata stored on disk, not RAM
   - Essential for large document collections

4. **HNSW On-Disk** (`HnswConfig.on_disk=True`)
   - Store the HNSW graph on disk
   - Trades search speed for memory

### Redis Optimizations

1. **Connection Pooling**
   - Reuse connections via `max_connections`
   - Reduces connection overhead

2. **Cache Strategies**
   - `CacheStrategy.CACHE_ASIDE`: Read-through with TTL
   - Metadata cached in Redis, vectors in Qdrant

## File Structure

```
scripts/
├── vector_cache.py          # Unified Redis + Qdrant client
├── Invoke-PythonScript.ps1  # PowerShell runner (includes vector_cache)

src/aspire_agents/_vendor/
├── _qdrant.py              # Qdrant vendor abstraction
├── _redis.py               # Redis vendor abstraction
├── _enums.py               # QdrantDistance, QdrantQuantization, etc.
└── __init__.py             # Exports all abstractions
```

## Usage

### PowerShell

```powershell
# Check Redis + Qdrant connectivity
.\scripts\Invoke-PythonScript.ps1 -ScriptName vector_cache

# With custom URLs
.\scripts\Invoke-PythonScript.ps1 -ScriptName vector_cache `
    -RedisUrl "redis://localhost:6379/0" `
    -QdrantUrl "http://localhost:6333" `
    -CollectionName "my_vectors"
```

### Python

```python
from scripts.vector_cache import VectorCache, RedisConfig, QdrantConfig

# Memory-optimized configuration
config = QdrantConfig(
    indexing_mode=IndexingMode.MMAP,
    quantization_enabled=True,
    on_disk_payload=True,
)

cache = VectorCache(qdrant_config=config)
cache.initialize()

# Store with metadata
cache.store(
    key="doc_123",
    vector=[0.1, 0.2, ...],  # 384-dim embedding
    metadata={"title": "Example", "source": "api"},
    ttl=3600  # 1 hour cache
)

# Search similar
results = cache.search_similar(query_vector, limit=10)
```

## Vendor Abstractions

### Qdrant Protocols

```python
from aspire_agents._vendor import (
    QdrantClientProtocol,
    AsyncQdrantClientProtocol,
    create_client,
    create_async_client,
    create_local_client,
)
```

### Qdrant Enums

```python
from aspire_agents._vendor import (
    QdrantDistance,        # Cosine, Euclid, Dot, Manhattan
    QdrantQuantization,    # SCALAR_INT8, PRODUCT, BINARY
    QdrantStorageMode,     # IN_MEMORY, MMAP, ON_DISK
    QdrantIndexType,       # DEFAULT (HNSW), FLAT
)
```

### Data Classes

```python
from aspire_agents._vendor import (
    VectorParams,
    HnswConfig,
    QuantizationConfig,
    OptimizersConfig,
    PointStruct,
    ScoredPoint,
    CollectionInfo,
)
```

## Python 3.15 Compatibility

Note: As of the current date, many packages (torch, qdrant-client, pydantic-core)
don't have Python 3.15 binary wheels. The vendor abstractions are designed to:

1. Work without the actual packages installed (type stubs only)
2. Use `importlib.import_module()` for lazy loading
3. Enable static type checking with protocols

When packages release 3.15 wheels, the runtime will work seamlessly.
