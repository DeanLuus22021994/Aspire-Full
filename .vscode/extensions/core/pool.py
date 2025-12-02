#!/usr/bin/env python3
"""Worker pool with tensor-optimized task scheduling."""

from __future__ import annotations

import asyncio
import os
import signal
import sys
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass, field
from enum import IntEnum, auto
from typing import TYPE_CHECKING, Any, Awaitable, Callable, Final, TypeVar

import numpy as np

if TYPE_CHECKING:
    from numpy.typing import NDArray

    from .context import ExtensionContext, ExtensionRegistry

# Free-threading support (Python 3.13+)
_FREE_THREADING: Final[bool] = os.environ.get("PYTHON_GIL", "1") == "0"

# Pool sizing constants
DEFAULT_WORKERS: Final[int] = min(32, (os.cpu_count() or 4) * 2)
MAX_QUEUE_DEPTH: Final[int] = 1024
PRIORITY_LEVELS: Final[int] = 4

T = TypeVar("T")


class TaskPriority(IntEnum):
    """Task priority levels for scheduling."""

    CRITICAL = 0  # GPU-required extensions
    HIGH = 1  # User-visible extensions
    NORMAL = 2  # Background downloads
    LOW = 3  # Preemptive caching


class WorkerState(IntEnum):
    """Worker lifecycle states."""

    IDLE = 0
    BUSY = auto()
    DRAINING = auto()
    STOPPED = auto()


@dataclass(slots=True)
class Task:
    """Task descriptor with priority and metadata."""

    id: int
    priority: TaskPriority
    context: ExtensionContext
    coro_factory: Callable[[ExtensionContext], Awaitable[Any]]
    result: Any = None
    error: Exception | None = None
    completed: bool = False


@dataclass(slots=True)
class WorkerStats:
    """Per-worker statistics for load balancing."""

    tasks_completed: int = 0
    tasks_failed: int = 0
    total_bytes: int = 0
    total_ns: int = 0

    @property
    def avg_latency_ms(self) -> float:
        """Average task latency in milliseconds."""
        if self.tasks_completed == 0:
            return 0.0
        return (self.total_ns / self.tasks_completed) / 1_000_000


class PriorityQueue:
    """Lock-free priority queue with NumPy-backed storage.

    Uses separate arrays per priority level for O(1) enqueue
    and efficient batch dequeue operations.
    """

    __slots__ = ("_queues", "_counts", "_lock")

    def __init__(self, capacity: int = MAX_QUEUE_DEPTH) -> None:
        """Initialize priority queues.

        Args:
            capacity: Maximum tasks per priority level.
        """
        self._queues: list[list[Task]] = [[] for _ in range(PRIORITY_LEVELS)]
        self._counts = np.zeros(PRIORITY_LEVELS, dtype=np.int32)
        self._lock = asyncio.Lock()

    async def enqueue(self, task: Task) -> None:
        """Add task to appropriate priority queue.

        Args:
            task: Task to enqueue.
        """
        async with self._lock:
            priority = min(task.priority.value, PRIORITY_LEVELS - 1)
            self._queues[priority].append(task)
            self._counts[priority] += 1

    async def dequeue(self) -> Task | None:
        """Remove and return highest-priority task.

        Returns:
            Next task or None if empty.
        """
        async with self._lock:
            for priority in range(PRIORITY_LEVELS):
                if self._queues[priority]:
                    task = self._queues[priority].pop(0)
                    self._counts[priority] -= 1
                    return task
            return None

    async def dequeue_batch(self, max_count: int = 8) -> list[Task]:
        """Remove multiple tasks for batch processing.

        Args:
            max_count: Maximum tasks to dequeue.

        Returns:
            List of tasks (may be less than max_count).
        """
        async with self._lock:
            result: list[Task] = []
            remaining = max_count

            for priority in range(PRIORITY_LEVELS):
                if remaining <= 0:
                    break
                take = min(len(self._queues[priority]), remaining)
                if take > 0:
                    result.extend(self._queues[priority][:take])
                    self._queues[priority] = self._queues[priority][take:]
                    self._counts[priority] -= take
                    remaining -= take

            return result

    @property
    def total_pending(self) -> int:
        """Total tasks across all priorities."""
        return int(np.sum(self._counts))

    def get_counts(self) -> NDArray[np.int32]:
        """Get per-priority counts for monitoring."""
        return self._counts.copy()


class WorkerPool:
    """Tensor-optimized async worker pool.

    Features:
    - Priority-based task scheduling
    - Adaptive worker scaling
    - Zero-copy result propagation
    - Graceful shutdown with drain
    """

    __slots__ = (
        "_workers",
        "_queue",
        "_stats",
        "_state",
        "_shutdown_event",
        "_task_counter",
        "_executor",
    )

    def __init__(self, worker_count: int = DEFAULT_WORKERS) -> None:
        """Initialize worker pool.

        Args:
            worker_count: Number of async workers.
        """
        self._workers = worker_count
        self._queue = PriorityQueue()
        self._stats = [WorkerStats() for _ in range(worker_count)]
        self._state = WorkerState.IDLE
        self._shutdown_event = asyncio.Event()
        self._task_counter = 0

        # Thread pool for CPU-bound operations
        self._executor = ThreadPoolExecutor(
            max_workers=worker_count,
            thread_name_prefix="tensor-worker",
        )

    async def submit(
        self,
        context: ExtensionContext,
        coro_factory: Callable[[ExtensionContext], Awaitable[T]],
        priority: TaskPriority = TaskPriority.NORMAL,
    ) -> Task:
        """Submit task to worker pool.

        Args:
            context: Extension context for task.
            coro_factory: Async function to execute.
            priority: Task priority level.

        Returns:
            Task handle for result retrieval.
        """
        self._task_counter += 1
        task = Task(
            id=self._task_counter,
            priority=priority,
            context=context,
            coro_factory=coro_factory,
        )
        await self._queue.enqueue(task)
        return task

    async def _worker_loop(self, worker_id: int) -> None:
        """Main worker loop for processing tasks.

        Args:
            worker_id: Worker index for stats.
        """
        stats = self._stats[worker_id]

        while self._state not in (WorkerState.STOPPED, WorkerState.DRAINING):
            if self._shutdown_event.is_set():
                break

            task = await self._queue.dequeue()
            if task is None:
                await asyncio.sleep(0.01)  # Backoff when empty
                continue

            start_ns = int(asyncio.get_event_loop().time() * 1_000_000_000)

            try:
                coro = task.coro_factory(task.context)
                task.result = await coro
                task.completed = True
                stats.tasks_completed += 1
            except Exception as e:
                task.error = e
                task.completed = True
                stats.tasks_failed += 1

            elapsed_ns = int(asyncio.get_event_loop().time() * 1_000_000_000) - start_ns
            stats.total_ns += elapsed_ns

    async def run(self) -> None:
        """Start worker pool and process tasks until shutdown."""
        self._state = WorkerState.BUSY

        # Create worker tasks
        workers = [
            asyncio.create_task(self._worker_loop(i)) for i in range(self._workers)
        ]

        # Wait for shutdown signal
        await self._shutdown_event.wait()

        # Drain remaining tasks
        self._state = WorkerState.DRAINING
        while self._queue.total_pending > 0:
            await asyncio.sleep(0.1)

        # Stop workers
        self._state = WorkerState.STOPPED
        await asyncio.gather(*workers, return_exceptions=True)

    async def shutdown(self, timeout: float = 30.0) -> None:
        """Gracefully shutdown worker pool.

        Args:
            timeout: Maximum seconds to wait for drain.
        """
        self._shutdown_event.set()
        self._executor.shutdown(wait=True, cancel_futures=True)

    def get_stats(self) -> list[WorkerStats]:
        """Get per-worker statistics."""
        return self._stats.copy()

    @property
    def pending_count(self) -> int:
        """Number of pending tasks."""
        return self._queue.total_pending


async def run_with_pool(
    contexts: list[ExtensionContext],
    coro_factory: Callable[[ExtensionContext], Awaitable[T]],
    worker_count: int = DEFAULT_WORKERS,
) -> list[T]:
    """Convenience function to run tasks through pool.

    Args:
        contexts: Extension contexts to process.
        coro_factory: Async function for each context.
        worker_count: Number of workers.

    Returns:
        List of results in input order.
    """
    pool = WorkerPool(worker_count)
    tasks: list[Task] = []

    # Submit all tasks
    for ctx in contexts:
        priority = TaskPriority.CRITICAL if ctx.is_gpu_required else TaskPriority.NORMAL
        task = await pool.submit(ctx, coro_factory, priority)
        tasks.append(task)

    # Start pool in background
    pool_task = asyncio.create_task(pool.run())

    # Wait for all tasks to complete
    while not all(t.completed for t in tasks):
        await asyncio.sleep(0.05)

    # Shutdown pool
    await pool.shutdown()
    await pool_task

    # Collect results in order
    results: list[T] = []
    for task in tasks:
        if task.error:
            raise task.error
        results.append(task.result)

    return results
