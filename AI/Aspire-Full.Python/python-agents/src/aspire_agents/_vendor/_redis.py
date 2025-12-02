"""Redis vendor abstractions.

Provides protocol definitions for the redis-py library,
enabling type checking without requiring the package to be installed.

Redis is an in-memory data structure store used as:
- Database
- Cache
- Message broker
- Streaming engine

This module abstracts the key Redis client interfaces.
"""

from __future__ import annotations

from types import TracebackType
from typing import (
    Any,
    Final,
    Generic,
    Protocol,
    TypeVar,
    runtime_checkable,
)

# ============================================================================
# Type Variables
# ============================================================================

T = TypeVar("T")
ResponseT = TypeVar("ResponseT")


# ============================================================================
# Exception Hierarchy
# ============================================================================


class RedisError(Exception):
    """Base exception for Redis operations.

    All Redis-specific exceptions inherit from this class.
    """

    pass


class RedisConnectionError(RedisError):
    """Exception raised for connection failures.

    Raised when:
    - Unable to connect to Redis server
    - Connection dropped unexpectedly
    - Network timeout during operation
    """

    pass


class RedisTimeoutError(RedisError):
    """Exception raised for operation timeouts.

    Raised when an operation exceeds its configured timeout.
    """

    pass


class AuthenticationError(RedisConnectionError):
    """Exception raised for authentication failures.

    Raised when:
    - Invalid password provided
    - ACL permissions denied
    - Authentication required but not provided
    """

    pass


class AuthenticationWrongNumberOfArgsError(AuthenticationError):
    """Exception raised when AUTH command has wrong arguments."""

    pass


class DataError(RedisError):
    """Exception raised for data-related errors.

    Raised when:
    - Invalid data type for operation
    - Data encoding/decoding fails
    - Command receives invalid arguments
    """

    pass


class InvalidResponse(RedisError):
    """Exception raised for protocol errors.

    Raised when the Redis server returns an unexpected response format.
    """

    pass


class ResponseError(RedisError):
    """Exception raised for Redis command errors.

    Raised when Redis returns an error response (ERR, WRONGTYPE, etc.)
    """

    pass


class BusyLoadingError(ResponseError):
    """Exception raised when Redis is loading the dataset.

    Raised during Redis startup while loading persistence data.
    """

    pass


class ReadOnlyError(ResponseError):
    """Exception raised on writes to read-only replicas.

    Raised when attempting to write to a replica configured as read-only.
    """

    pass


class PubSubError(RedisError):
    """Exception raised for Pub/Sub errors.

    Raised when:
    - Invalid Pub/Sub command sequence
    - Channel subscription failures
    """

    pass


class WatchError(RedisError):
    """Exception raised when WATCH key is modified.

    Raised when a transaction fails due to key modification
    during an optimistic locking operation.
    """

    pass


class ChildDeadlockedError(RedisError):
    """Exception raised for BGSAVE/BGREWRITEAOF deadlock.

    Raised when a background save operation deadlocks.
    """

    pass


# ============================================================================
# Credential Provider Protocol
# ============================================================================


@runtime_checkable
class CredentialProviderProtocol(Protocol):
    """Protocol for Redis credential providers.

    Allows dynamic credential resolution for authentication.
    """

    def get_credentials(self) -> tuple[str, str] | tuple[str]:
        """Get credentials for Redis authentication.

        Returns:
            Tuple of (username, password) or (password,)
        """
        ...


# ============================================================================
# Connection Protocol
# ============================================================================


@runtime_checkable
class ConnectionProtocol(Protocol):
    """Protocol for Redis connection.

    Represents a single connection to a Redis server.
    """

    def connect(self) -> None:
        """Establish connection to Redis server."""
        ...

    def disconnect(self, nowait: bool = False) -> None:
        """Disconnect from Redis server.

        Args:
            nowait: If True, don't wait for pending responses
        """
        ...

    def can_read(self, timeout: float = 0) -> bool:
        """Check if data is available to read.

        Args:
            timeout: How long to wait for data

        Returns:
            True if data is available
        """
        ...

    def send_command(self, *args: Any, **kwargs: Any) -> None:
        """Send a command to Redis.

        Args:
            *args: Command and arguments
            **kwargs: Additional options
        """
        ...

    def read_response(
        self,
        disable_decoding: bool = False,
    ) -> Any:
        """Read a response from Redis.

        Args:
            disable_decoding: If True, return raw bytes

        Returns:
            Response from Redis server
        """
        ...


# ============================================================================
# Connection Pool Protocols
# ============================================================================


@runtime_checkable
class ConnectionPoolProtocol(Protocol):
    """Protocol for Redis connection pool.

    Manages a pool of connections for efficient connection reuse.
    """

    def get_connection(
        self,
        command_name: str,
        *keys: Any,
        **options: Any,
    ) -> ConnectionProtocol:
        """Get a connection from the pool.

        Args:
            command_name: Name of the command to execute
            *keys: Key arguments for routing
            **options: Additional options

        Returns:
            A connection from the pool
        """
        ...

    def release(self, connection: ConnectionProtocol) -> None:
        """Return a connection to the pool.

        Args:
            connection: Connection to release
        """
        ...

    def disconnect(self, inuse_connections: bool = True) -> None:
        """Disconnect all connections in the pool.

        Args:
            inuse_connections: Also disconnect in-use connections
        """
        ...


@runtime_checkable
class BlockingConnectionPoolProtocol(ConnectionPoolProtocol, Protocol):
    """Protocol for blocking connection pool.

    Blocks when all connections are in use rather than creating new ones.
    """

    pass


# ============================================================================
# Sentinel Protocols
# ============================================================================


@runtime_checkable
class SentinelProtocol(Protocol):
    """Protocol for Redis Sentinel client.

    Provides high-availability support through Sentinel.
    """

    def discover_master(self, service_name: str) -> tuple[str, int]:
        """Discover the master for a service.

        Args:
            service_name: Name of the monitored service

        Returns:
            Tuple of (host, port) for the master
        """
        ...

    def discover_slaves(
        self,
        service_name: str,
    ) -> list[tuple[str, int]]:
        """Discover slaves for a service.

        Args:
            service_name: Name of the monitored service

        Returns:
            List of (host, port) tuples for slaves
        """
        ...

    def master_for(
        self,
        service_name: str,
        **kwargs: Any,
    ) -> "RedisProtocol[Any]":
        """Get a Redis client for the master.

        Args:
            service_name: Name of the monitored service
            **kwargs: Additional Redis client options

        Returns:
            Redis client connected to the master
        """
        ...

    def slave_for(
        self,
        service_name: str,
        **kwargs: Any,
    ) -> "RedisProtocol[Any]":
        """Get a Redis client for a slave.

        Args:
            service_name: Name of the monitored service
            **kwargs: Additional Redis client options

        Returns:
            Redis client connected to a slave
        """
        ...


@runtime_checkable
class SentinelConnectionPoolProtocol(ConnectionPoolProtocol, Protocol):
    """Protocol for Sentinel-aware connection pool.

    Automatically discovers and connects to the correct node.
    """

    pass


# ============================================================================
# Redis Client Protocol
# ============================================================================


@runtime_checkable
class RedisProtocol(Protocol, Generic[ResponseT]):
    """Protocol for Redis client.

    The main interface for interacting with Redis.
    Supports both sync and async operations.
    """

    # Connection management
    def close(self) -> None:
        """Close the Redis connection."""
        ...

    def ping(self) -> bool:
        """Ping the Redis server.

        Returns:
            True if server responds
        """
        ...

    # String operations
    def get(self, name: str) -> ResponseT | None:
        """Get the value of a key.

        Args:
            name: Key name

        Returns:
            Value or None if key doesn't exist
        """
        ...

    def set(
        self,
        name: str,
        value: Any,
        ex: int | None = None,
        px: int | None = None,
        nx: bool = False,
        xx: bool = False,
    ) -> bool | None:
        """Set a key's value.

        Args:
            name: Key name
            value: Value to set
            ex: Expiry in seconds
            px: Expiry in milliseconds
            nx: Only set if key doesn't exist
            xx: Only set if key exists

        Returns:
            True if set, None if condition not met
        """
        ...

    def delete(self, *names: str) -> int:
        """Delete one or more keys.

        Args:
            *names: Key names to delete

        Returns:
            Number of keys deleted
        """
        ...

    def exists(self, *names: str) -> int:
        """Check if keys exist.

        Args:
            *names: Key names to check

        Returns:
            Number of keys that exist
        """
        ...

    def expire(self, name: str, time: int) -> bool:
        """Set expiry on a key.

        Args:
            name: Key name
            time: Expiry time in seconds

        Returns:
            True if expiry was set
        """
        ...

    def ttl(self, name: str) -> int:
        """Get time-to-live for a key.

        Args:
            name: Key name

        Returns:
            TTL in seconds, -1 if no expiry, -2 if key doesn't exist
        """
        ...

    # Hash operations
    def hget(self, name: str, key: str) -> ResponseT | None:
        """Get a hash field value."""
        ...

    def hset(
        self,
        name: str,
        key: str | None = None,
        value: Any = None,
        mapping: dict[str, Any] | None = None,
    ) -> int:
        """Set hash field(s)."""
        ...

    def hgetall(self, name: str) -> dict[str, ResponseT]:
        """Get all hash fields and values."""
        ...

    # List operations
    def lpush(self, name: str, *values: Any) -> int:
        """Push values to the left of a list."""
        ...

    def rpush(self, name: str, *values: Any) -> int:
        """Push values to the right of a list."""
        ...

    def lpop(self, name: str, count: int | None = None) -> ResponseT | list[ResponseT] | None:
        """Pop values from the left of a list."""
        ...

    def lrange(self, name: str, start: int, end: int) -> list[ResponseT]:
        """Get a range of list elements."""
        ...

    # Set operations
    def sadd(self, name: str, *values: Any) -> int:
        """Add members to a set."""
        ...

    def smembers(self, name: str) -> set[ResponseT]:
        """Get all members of a set."""
        ...

    # Pipeline and transaction
    def pipeline(
        self,
        transaction: bool = True,
        shard_hint: str | None = None,
    ) -> "PipelineProtocol":
        """Create a pipeline for batch operations.

        Args:
            transaction: Wrap in MULTI/EXEC
            shard_hint: Hint for cluster routing

        Returns:
            Pipeline object
        """
        ...

    # Pub/Sub
    def pubsub(self, **kwargs: Any) -> "PubSubProtocol":
        """Create a Pub/Sub object.

        Returns:
            PubSub object for subscriptions
        """
        ...

    def publish(self, channel: str, message: Any) -> int:
        """Publish a message to a channel.

        Args:
            channel: Channel name
            message: Message to publish

        Returns:
            Number of subscribers that received the message
        """
        ...

    # Context manager
    def __enter__(self) -> "RedisProtocol[ResponseT]":
        """Enter context manager."""
        ...

    def __exit__(
        self,
        __exc_type: type[BaseException] | None,
        __exc_val: BaseException | None,
        __exc_tb: TracebackType | None,
        /,
    ) -> None:
        """Exit context manager."""
        ...


# ============================================================================
# Pipeline Protocol
# ============================================================================


@runtime_checkable
class PipelineProtocol(Protocol):
    """Protocol for Redis pipeline.

    Batches multiple commands for efficient execution.
    """

    def execute(
        self,
        raise_on_error: bool = True,
    ) -> list[Any]:
        """Execute all queued commands.

        Args:
            raise_on_error: Raise exception on any error

        Returns:
            List of responses for each command
        """
        ...

    def watch(self, *names: str) -> bool:
        """Watch keys for optimistic locking.

        Args:
            *names: Key names to watch

        Returns:
            True
        """
        ...

    def multi(self) -> None:
        """Start a transaction block."""
        ...


# ============================================================================
# PubSub Protocol
# ============================================================================


@runtime_checkable
class PubSubProtocol(Protocol):
    """Protocol for Redis Pub/Sub.

    Handles subscription and message delivery.
    """

    def subscribe(self, *args: str, **kwargs: Any) -> None:
        """Subscribe to channels.

        Args:
            *args: Channel names
            **kwargs: Additional options
        """
        ...

    def unsubscribe(self, *args: str) -> None:
        """Unsubscribe from channels.

        Args:
            *args: Channel names (empty for all)
        """
        ...

    def psubscribe(self, *args: str, **kwargs: Any) -> None:
        """Subscribe to channel patterns.

        Args:
            *args: Channel patterns (glob-style)
            **kwargs: Additional options
        """
        ...

    def punsubscribe(self, *args: str) -> None:
        """Unsubscribe from patterns.

        Args:
            *args: Patterns (empty for all)
        """
        ...

    def get_message(
        self,
        ignore_subscribe_messages: bool = False,
        timeout: float = 0.0,
    ) -> dict[str, Any] | None:
        """Get the next message.

        Args:
            ignore_subscribe_messages: Skip subscription confirmations
            timeout: How long to wait for a message

        Returns:
            Message dict or None
        """
        ...

    def listen(self) -> Any:
        """Generator yielding messages.

        Yields:
            Message dictionaries
        """
        ...

    def close(self) -> None:
        """Close the Pub/Sub connection."""
        ...


# ============================================================================
# Factory Functions
# ============================================================================


def from_url(
    url: str,
    db: int = 0,
    decode_responses: bool = False,
    **kwargs: Any,
) -> RedisProtocol[Any]:
    """Create a Redis client from a URL.

    Args:
        url: Redis URL (redis://host:port/db)
        db: Database number (overrides URL)
        decode_responses: Decode bytes to strings
        **kwargs: Additional Redis options

    Returns:
        Redis client instance
    """
    ...


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Exceptions
    "RedisError",
    "RedisConnectionError",
    "RedisTimeoutError",
    "AuthenticationError",
    "AuthenticationWrongNumberOfArgsError",
    "DataError",
    "InvalidResponse",
    "ResponseError",
    "BusyLoadingError",
    "ReadOnlyError",
    "PubSubError",
    "WatchError",
    "ChildDeadlockedError",
    # Protocols
    "CredentialProviderProtocol",
    "ConnectionProtocol",
    "ConnectionPoolProtocol",
    "BlockingConnectionPoolProtocol",
    "SentinelProtocol",
    "SentinelConnectionPoolProtocol",
    "RedisProtocol",
    "PipelineProtocol",
    "PubSubProtocol",
    # Factory Functions
    "from_url",
]
