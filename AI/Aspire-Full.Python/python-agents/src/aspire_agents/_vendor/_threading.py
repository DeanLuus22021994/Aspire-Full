"""Python 3.15 Free-Threading abstractions.

Provides protocol definitions and utilities for Python 3.15's free-threaded
(GIL-disabled) runtime without requiring the actual Python 3.15 environment.

Key Python 3.15 Free-Threading Features:
- PYTHON_GIL=0 environment variable to disable the GIL
- _thread.LockType for low-level lock operations
- _thread._ThreadHandle for joinable thread management
- threading.Lock, RLock, Condition, Event, Semaphore, Barrier
- concurrent.futures.ThreadPoolExecutor with improved parallelism
"""

from __future__ import annotations

import sys
from collections.abc import Callable, Iterable
from concurrent.futures import Future
from dataclasses import dataclass
from threading import Thread as _Thread
from types import TracebackType
from typing import (
    Any,
    Final,
    Protocol,
    TypeVar,
    cast,
    runtime_checkable,
)

# ============================================================================
# Type Variables
# ============================================================================

T = TypeVar("T")
T_Executor = TypeVar("T_Executor")  # Invariant for protocols


# ============================================================================
# GIL State Detection
# ============================================================================


def is_gil_disabled() -> bool:
    """Check if the GIL is disabled (free-threaded mode).

    Returns:
        True if running in free-threaded mode (PYTHON_GIL=0)
    """
    # Python 3.13+ has sys._is_gil_enabled()
    is_gil_enabled = getattr(sys, "_is_gil_enabled", None)
    if is_gil_enabled is not None:
        return not is_gil_enabled()
    return False


def get_python_version() -> tuple[int, int, int]:
    """Get Python version tuple.

    Returns:
        Tuple of (major, minor, micro) version numbers
    """
    return sys.version_info[:3]


def supports_free_threading() -> bool:
    """Check if current Python version supports free-threading.

    Returns:
        True if Python version >= 3.13 (free-threading capable)
    """
    return sys.version_info >= (3, 13)


# ============================================================================
# Low-Level Thread Lock Protocol (_thread.LockType)
# ============================================================================


@runtime_checkable
class LockTypeProtocol(Protocol):
    """Protocol for _thread.LockType.

    Low-level lock implementation for thread synchronization.
    """

    def acquire(self, blocking: bool = True, timeout: float = -1) -> bool:
        """Acquire the lock.

        Args:
            blocking: Whether to block until lock is available
            timeout: Maximum time to wait (-1 for infinite)

        Returns:
            True if lock was acquired
        """
        ...

    def acquire_lock(self, blocking: bool = True, timeout: float = -1) -> bool:
        """Alias for acquire()."""
        ...

    def release(self) -> None:
        """Release the lock."""
        ...

    def release_lock(self) -> None:
        """Alias for release()."""
        ...

    def locked(self) -> bool:
        """Check if lock is held.

        Returns:
            True if lock is currently held
        """
        ...

    def locked_lock(self) -> bool:
        """Alias for locked()."""
        ...

    def __enter__(self) -> bool:
        """Context manager entry."""
        ...

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: TracebackType | None,
    ) -> None:
        """Context manager exit."""
        ...


# ============================================================================
# Thread Handle Protocol (_thread._ThreadHandle) - Python 3.15+
# ============================================================================


@runtime_checkable
class ThreadHandleProtocol(Protocol):
    """Protocol for _thread._ThreadHandle (Python 3.15+).

    Handle for managing joinable threads at the low level.
    """

    @property
    def ident(self) -> int:
        """Thread identifier."""
        ...

    def is_done(self) -> bool:
        """Check if thread has completed.

        Returns:
            True if thread has finished execution
        """
        ...

    def join(self, timeout: float | None = None) -> None:
        """Wait for thread to complete.

        Args:
            timeout: Maximum time to wait (None for infinite)
        """
        ...


# ============================================================================
# Threading Lock Protocol
# ============================================================================


@runtime_checkable
class LockProtocol(Protocol):
    """Protocol for threading.Lock.

    Simple mutex lock for thread synchronization.
    """

    def acquire(self, blocking: bool = True, timeout: float = -1) -> bool:
        """Acquire the lock.

        Args:
            blocking: Whether to block until lock is available
            timeout: Maximum time to wait (-1 for infinite)

        Returns:
            True if lock was acquired
        """
        ...

    def release(self) -> None:
        """Release the lock."""
        ...

    def locked(self) -> bool:
        """Check if lock is held."""
        ...

    def __enter__(self) -> bool:
        """Context manager entry."""
        ...

    def __exit__(
        self,
        __exc_type: type[BaseException] | None,
        __exc_val: BaseException | None,
        __exc_tb: TracebackType | None,
        /,
    ) -> None:
        """Context manager exit."""
        ...


# ============================================================================
# RLock Protocol
# ============================================================================


@runtime_checkable
class RLockProtocol(Protocol):
    """Protocol for threading.RLock (reentrant lock).

    Can be acquired multiple times by the same thread.
    """

    def acquire(self, blocking: bool = True, timeout: float = -1) -> bool:
        """Acquire the lock.

        Args:
            blocking: Whether to block until lock is available
            timeout: Maximum time to wait (-1 for infinite)

        Returns:
            True if lock was acquired
        """
        ...

    def release(self) -> None:
        """Release one level of lock ownership."""
        ...

    def __enter__(self) -> bool:
        """Context manager entry."""
        ...

    def __exit__(
        self,
        __exc_type: type[BaseException] | None,
        __exc_val: BaseException | None,
        __exc_tb: TracebackType | None,
        /,
    ) -> None:
        """Context manager exit."""
        ...


# ============================================================================
# Condition Protocol
# ============================================================================


@runtime_checkable
class ConditionProtocol(Protocol):
    """Protocol for threading.Condition.

    Condition variable for complex thread synchronization.
    """

    def acquire(self, blocking: bool = True, timeout: float = -1) -> bool:
        """Acquire the underlying lock."""
        ...

    def release(self) -> None:
        """Release the underlying lock."""
        ...

    def wait(self, timeout: float | None = None) -> bool:
        """Wait for notification.

        Args:
            timeout: Maximum time to wait (None for infinite)

        Returns:
            True if notified before timeout
        """
        ...

    def wait_for(
        self,
        predicate: Callable[[], T],
        timeout: float | None = None,
    ) -> T:
        """Wait until predicate returns True.

        Args:
            predicate: Function to check condition
            timeout: Maximum time to wait

        Returns:
            Result of predicate (truthy value)
        """
        ...

    def notify(self, n: int = 1) -> None:
        """Wake up n waiting threads.

        Args:
            n: Number of threads to wake
        """
        ...

    def notify_all(self) -> None:
        """Wake up all waiting threads."""
        ...

    def __enter__(self) -> bool:
        """Context manager entry."""
        ...

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: TracebackType | None,
    ) -> None:
        """Context manager exit."""
        ...


# ============================================================================
# Event Protocol
# ============================================================================


@runtime_checkable
class EventProtocol(Protocol):
    """Protocol for threading.Event.

    Simple event for thread signaling.
    """

    def set(self) -> None:
        """Set the internal flag to True."""
        ...

    def clear(self) -> None:
        """Reset the internal flag to False."""
        ...

    def is_set(self) -> bool:
        """Check if flag is set.

        Returns:
            True if flag is set
        """
        ...

    def wait(self, timeout: float | None = None) -> bool:
        """Block until flag is set.

        Args:
            timeout: Maximum time to wait (None for infinite)

        Returns:
            True if flag was set before timeout
        """
        ...


# ============================================================================
# Semaphore Protocol
# ============================================================================


@runtime_checkable
class SemaphoreProtocol(Protocol):
    """Protocol for threading.Semaphore.

    Counting semaphore for limiting concurrent access.
    """

    def acquire(
        self,
        blocking: bool = True,
        timeout: float | None = None,
    ) -> bool:
        """Acquire the semaphore.

        Args:
            blocking: Whether to block until available
            timeout: Maximum time to wait

        Returns:
            True if semaphore was acquired
        """
        ...

    def release(self, n: int = 1) -> None:
        """Release the semaphore.

        Args:
            n: Number of releases (Python 3.9+)
        """
        ...

    def __enter__(self) -> bool:
        """Context manager entry."""
        ...

    def __exit__(
        self,
        __exc_type: type[BaseException] | None,
        __exc_val: BaseException | None,
        __exc_tb: TracebackType | None,
        /,
    ) -> None:
        """Context manager exit."""
        ...


# ============================================================================
# Barrier Protocol
# ============================================================================


@runtime_checkable
class BarrierProtocol(Protocol):
    """Protocol for threading.Barrier.

    Synchronization barrier for coordinating threads.
    """

    @property
    def parties(self) -> int:
        """Number of threads required to pass barrier."""
        ...

    @property
    def n_waiting(self) -> int:
        """Number of threads currently waiting."""
        ...

    @property
    def broken(self) -> bool:
        """True if barrier is in broken state."""
        ...

    def wait(self, timeout: float | None = None) -> int:
        """Wait at the barrier.

        Args:
            timeout: Maximum time to wait

        Returns:
            Arrival index (0 to parties-1)
        """
        ...

    def reset(self) -> None:
        """Reset the barrier to initial state."""
        ...

    def abort(self) -> None:
        """Put the barrier in broken state."""
        ...


# ============================================================================
# Thread Protocol
# ============================================================================


@runtime_checkable
class ThreadProtocol(Protocol):
    """Protocol for threading.Thread.

    High-level thread abstraction.
    """

    @property
    def name(self) -> str:
        """Thread name."""
        ...

    @name.setter
    def name(self, value: str) -> None:
        """Set thread name."""
        ...

    @property
    def ident(self) -> int | None:
        """Thread identifier (None before start)."""
        ...

    @property
    def native_id(self) -> int | None:
        """Native thread ID (None before start)."""
        ...

    @property
    def daemon(self) -> bool:
        """Whether thread is daemon thread."""
        ...

    @daemon.setter
    def daemon(self, value: bool) -> None:
        """Set daemon status."""
        ...

    def start(self) -> None:
        """Start the thread."""
        ...

    def run(self) -> None:
        """Thread entry point (override in subclass)."""
        ...

    def join(self, timeout: float | None = None) -> None:
        """Wait for thread to complete.

        Args:
            timeout: Maximum time to wait
        """
        ...

    def is_alive(self) -> bool:
        """Check if thread is running.

        Returns:
            True if thread is running
        """
        ...


# ============================================================================
# ThreadPoolExecutor Protocol
# ============================================================================


@runtime_checkable
class ThreadPoolExecutorProtocol(Protocol):
    """Protocol for concurrent.futures.ThreadPoolExecutor.

    Thread pool for parallel task execution.
    In Python 3.15+ with GIL disabled, provides true parallelism.
    """

    def submit(
        self,
        fn: Callable[..., T],
        /,
        *args: Any,
        **kwargs: Any,
    ) -> "Future[T]":
        """Submit a callable to be executed.

        Args:
            fn: Callable to execute
            *args: Positional arguments
            **kwargs: Keyword arguments

        Returns:
            Future representing the execution
        """
        ...

    def map(
        self,
        fn: Callable[..., T],
        *iterables: Iterable[Any],
        timeout: float | None = None,
        chunksize: int = 1,
    ) -> Iterable[T]:
        """Map a callable across iterables.

        Args:
            fn: Callable to apply
            *iterables: Input iterables
            timeout: Maximum time to wait
            chunksize: Batch size (for ProcessPoolExecutor)

        Returns:
            Iterator of results
        """
        ...

    def shutdown(
        self,
        wait: bool = True,
        *,
        cancel_futures: bool = False,
    ) -> None:
        """Shutdown the executor.

        Args:
            wait: Whether to wait for pending futures
            cancel_futures: Whether to cancel pending futures
        """
        ...

    def __enter__(self) -> "ThreadPoolExecutorProtocol":
        """Context manager entry."""
        ...

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: TracebackType | None,
    ) -> bool | None:
        """Context manager exit."""
        ...


# ============================================================================
# Future Protocol
# ============================================================================


@runtime_checkable
class FutureProtocol(Protocol):
    """Protocol for concurrent.futures.Future.

    Represents the result of an asynchronous computation.
    """

    def cancel(self) -> bool:
        """Attempt to cancel the future.

        Returns:
            True if cancelled successfully
        """
        ...

    def cancelled(self) -> bool:
        """Check if future was cancelled.

        Returns:
            True if cancelled
        """
        ...

    def running(self) -> bool:
        """Check if future is running.

        Returns:
            True if running
        """
        ...

    def done(self) -> bool:
        """Check if future is complete.

        Returns:
            True if done (completed, cancelled, or raised)
        """
        ...

    def result(self, timeout: float | None = None) -> Any:
        """Get the result.

        Args:
            timeout: Maximum time to wait

        Returns:
            Result value

        Raises:
            CancelledError: If cancelled
            TimeoutError: If timeout exceeded
            Exception: If computation raised
        """
        ...

    def exception(self, timeout: float | None = None) -> BaseException | None:
        """Get the exception if any.

        Args:
            timeout: Maximum time to wait

        Returns:
            Exception raised or None
        """
        ...

    def add_done_callback(self, fn: Callable[[Any], None]) -> None:
        """Add callback to run when future completes.

        Args:
            fn: Callback function
        """
        ...


# ============================================================================
# Thread-Local Storage
# ============================================================================


@runtime_checkable
class ThreadLocalProtocol(Protocol):
    """Protocol for threading.local / _thread._local.

    Thread-local storage for per-thread data.
    Note: Actual attribute access is dynamic per-thread.
    """


# ============================================================================
# Exception Hook Args (for threading.excepthook)
# ============================================================================


@dataclass(frozen=True)
class ExceptHookArgs:
    """Arguments for threading.excepthook.

    Contains exception information from thread failures.
    """

    exc_type: type[BaseException]
    """Exception type."""

    exc_value: BaseException | None
    """Exception instance."""

    exc_traceback: TracebackType | None
    """Exception traceback."""

    thread: _Thread | None
    """Thread that raised the exception."""


# ============================================================================
# Free-Threading Configuration
# ============================================================================


@dataclass
class FreeThreadingConfig:
    """Configuration for free-threaded execution.

    Provides settings for optimizing concurrent execution
    in Python 3.15+ with GIL disabled.
    """

    max_workers: int | None = None
    """Maximum worker threads (None = CPU count)."""

    thread_name_prefix: str = ""
    """Prefix for worker thread names."""

    use_daemon_threads: bool = True
    """Whether worker threads are daemon threads."""

    enable_tracing: bool = False
    """Enable thread execution tracing."""

    stack_size: int = 0
    """Stack size for threads (0 = default)."""

    @classmethod
    def for_cpu_bound(cls) -> "FreeThreadingConfig":
        """Config optimized for CPU-bound work."""
        import os

        return cls(
            max_workers=os.cpu_count(),
            thread_name_prefix="cpu-worker-",
            use_daemon_threads=True,
        )

    @classmethod
    def for_io_bound(cls, multiplier: int = 5) -> "FreeThreadingConfig":
        """Config optimized for I/O-bound work.

        Args:
            multiplier: Worker count multiplier over CPU count
        """
        import os

        cpu_count = os.cpu_count() or 1
        return cls(
            max_workers=cpu_count * multiplier,
            thread_name_prefix="io-worker-",
            use_daemon_threads=True,
        )


# ============================================================================
# Factory Functions
# ============================================================================


def create_thread_pool(
    config: FreeThreadingConfig | None = None,
) -> ThreadPoolExecutorProtocol:
    """Create a thread pool executor.

    Args:
        config: Threading configuration

    Returns:
        ThreadPoolExecutor instance
    """
    from concurrent.futures import ThreadPoolExecutor

    if config is None:
        config = FreeThreadingConfig()

    return ThreadPoolExecutor(
        max_workers=config.max_workers,
        thread_name_prefix=config.thread_name_prefix,
    )


def create_lock() -> LockProtocol:
    """Create a threading Lock.

    Returns:
        Lock instance
    """
    from threading import Lock

    return Lock()


def create_rlock() -> RLockProtocol:
    """Create a threading RLock.

    Returns:
        RLock instance
    """
    from threading import RLock

    return RLock()


def create_event() -> EventProtocol:
    """Create a threading Event.

    Returns:
        Event instance
    """
    from threading import Event

    return Event()


def create_semaphore(value: int = 1) -> SemaphoreProtocol:
    """Create a threading Semaphore.

    Args:
        value: Initial semaphore value

    Returns:
        Semaphore instance
    """
    from threading import Semaphore

    return Semaphore(value)


def create_condition(
    lock: LockProtocol | RLockProtocol | None = None,
) -> ConditionProtocol:
    """Create a threading Condition.

    Args:
        lock: Optional lock to use

    Returns:
        Condition instance implementing ConditionProtocol.
    """
    from threading import Condition, Lock, RLock

    # Convert protocol types to stdlib types
    if lock is None:
        stdlib_lock = None
    elif isinstance(lock, (Lock, RLock)):
        stdlib_lock = lock
    else:
        # Protocol-compatible lock - use cast for type assertion
        stdlib_lock = cast(Lock | RLock, lock)

    return cast(ConditionProtocol, Condition(stdlib_lock))


def create_barrier(
    parties: int,
    action: Callable[[], None] | None = None,
    timeout: float | None = None,
) -> BarrierProtocol:
    """Create a threading Barrier.

    Args:
        parties: Number of threads to synchronize
        action: Optional action to run at barrier
        timeout: Default timeout for wait()

    Returns:
        Barrier instance
    """
    from threading import Barrier

    return Barrier(parties, action, timeout)


# ============================================================================
# Utility Functions
# ============================================================================


def get_active_thread_count() -> int:
    """Get number of active threads.

    Returns:
        Number of active Thread objects
    """
    from threading import active_count

    return active_count()


def get_current_thread() -> ThreadProtocol:
    """Get the current thread.

    Returns:
        Current Thread object
    """
    from threading import current_thread

    return current_thread()


def get_main_thread() -> ThreadProtocol:
    """Get the main thread.

    Returns:
        Main Thread object
    """
    from threading import main_thread

    return main_thread()


def enumerate_threads() -> list[ThreadProtocol]:
    """Get list of all active threads.

    Returns:
        List of active Thread objects
    """
    from threading import enumerate as thread_enumerate

    return list(thread_enumerate())


# ============================================================================
# Constants
# ============================================================================

TIMEOUT_MAX: Final[float] = float("inf")
"""Maximum timeout value for thread operations."""


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # GIL Detection
    "is_gil_disabled",
    "get_python_version",
    "supports_free_threading",
    # Lock Protocols
    "LockTypeProtocol",
    "LockProtocol",
    "RLockProtocol",
    # Synchronization Protocols
    "ConditionProtocol",
    "EventProtocol",
    "SemaphoreProtocol",
    "BarrierProtocol",
    # Thread Protocols
    "ThreadHandleProtocol",
    "ThreadProtocol",
    "ThreadLocalProtocol",
    # Executor Protocols
    "ThreadPoolExecutorProtocol",
    "FutureProtocol",
    # Data Classes
    "ExceptHookArgs",
    "FreeThreadingConfig",
    # Factories
    "create_thread_pool",
    "create_lock",
    "create_rlock",
    "create_event",
    "create_semaphore",
    "create_condition",
    "create_barrier",
    # Utilities
    "get_active_thread_count",
    "get_current_thread",
    "get_main_thread",
    "enumerate_threads",
    # Constants
    "TIMEOUT_MAX",
]
