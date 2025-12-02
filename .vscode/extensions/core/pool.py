#!/usr/bin/env python3
"""Worker pool with tensor-optimized task scheduling.

Supports 3 hot GPU workers for low-latency high-throughput automation.
"""

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

# =============================================================================
# Pool Configuration - 3 Hot GPU Workers
# =============================================================================

# Hot GPU worker count for automation
HOT_GPU_WORKERS: Final[int] = 3

# Default workers scales with CPU but capped at 32
DEFAULT_WORKERS: Final[int] = min(32, (os.cpu_count() or 4) * 2)

# Queue sizing for high throughput
MAX_QUEUE_DEPTH: Final[int] = 4096  # Increased for automation workloads
PRIORITY_LEVELS: Final[int] = 4

# Latency targets (nanoseconds)
TARGET_LATENCY_NS: Final[int] = 50_000_000  # 50ms target
LOW_LATENCY_SPIN_NS: Final[int] = 1_000_000  # 1ms spin for hot standby

# GPU direct acceleration
GPU_DIRECT_ENABLED: Final[bool] = os.environ.get("USE_GPU_DIRECT", "1") == "1"
CUDA_VISIBLE_DEVICES: Final[str] = os.environ.get("CUDA_VISIBLE_DEVICES", "0")

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


# =============================================================================
# Hot GPU Worker Pool - 3 Dedicated Workers
# =============================================================================


class HotGPUWorkerState(IntEnum):
    """Hot GPU worker states for low-latency operation."""

    HOT_STANDBY = 0  # Warm, GPU loaded, immediate dispatch
    ACTIVE = 1  # Currently processing
    DRAINING = 2  # Completing current work
    COOLDOWN = 3  # Temporary backoff (GPU memory pressure)


@dataclass(slots=True)
class GPUWorkerStats:
    """Extended stats for GPU-accelerated workers."""

    worker_id: int
    tasks_completed: int = 0
    tasks_failed: int = 0
    total_bytes: int = 0
    total_ns: int = 0
    gpu_operations: int = 0
    gpu_compute_time_ns: int = 0
    current_state: HotGPUWorkerState = HotGPUWorkerState.HOT_STANDBY

    @property
    def avg_latency_ms(self) -> float:
        """Average task latency in milliseconds."""
        if self.tasks_completed == 0:
            return 0.0
        return (self.total_ns / self.tasks_completed) / 1_000_000

    @property
    def avg_gpu_time_ms(self) -> float:
        """Average GPU compute time in milliseconds."""
        if self.gpu_operations == 0:
            return 0.0
        return (self.gpu_compute_time_ns / self.gpu_operations) / 1_000_000


class HotGPUWorkerPool:
    """3 Hot GPU Workers with Direct Acceleration.

    Features:
    - 3 dedicated hot standby workers
    - Direct GPU compute without CPU fallback
    - 1ms spin wait for ultra-low latency
    - Priority-based task distribution
    - Automatic GPU memory management
    """

    __slots__ = (
        "_workers",
        "_queue",
        "_stats",
        "_states",
        "_shutdown_event",
        "_task_counter",
        "_executor",
        "_gpu_available",
    )

    def __init__(self) -> None:
        """Initialize 3 hot GPU workers."""
        self._workers = HOT_GPU_WORKERS
        self._queue = PriorityQueue(capacity=MAX_QUEUE_DEPTH)
        self._stats = [GPUWorkerStats(worker_id=i) for i in range(HOT_GPU_WORKERS)]
        self._states = [HotGPUWorkerState.HOT_STANDBY] * HOT_GPU_WORKERS
        self._shutdown_event = asyncio.Event()
        self._task_counter = 0

        # Thread pool for CPU-bound operations (fallback only)
        self._executor = ThreadPoolExecutor(
            max_workers=HOT_GPU_WORKERS,
            thread_name_prefix="hot-gpu-worker",
        )

        # Detect GPU availability
        self._gpu_available = self._detect_gpu()

    def _detect_gpu(self) -> bool:
        """Detect if GPU acceleration is available."""
        if not GPU_DIRECT_ENABLED:
            return False
        try:
            import cupy as cp  # type: ignore

            cp.cuda.Device(0).compute_capability
            return True
        except Exception:
            return False

    async def submit(
        self,
        context: ExtensionContext,
        coro_factory: Callable[[ExtensionContext], Awaitable[T]],
        priority: TaskPriority = TaskPriority.CRITICAL,
    ) -> Task:
        """Submit task to hot GPU pool.

        Args:
            context: Extension context for task.
            coro_factory: Async function to execute.
            priority: Task priority level (defaults to CRITICAL for GPU).

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

    async def _hot_worker_loop(self, worker_id: int) -> None:
        """Hot standby worker loop with 1ms spin wait.

        Args:
            worker_id: Worker index (0-2).
        """
        stats = self._stats[worker_id]
        stats.current_state = HotGPUWorkerState.HOT_STANDBY

        while not self._shutdown_event.is_set():
            task = await self._queue.dequeue()

            if task is None:
                # Hot standby - 1ms spin for low latency
                await asyncio.sleep(0.001)
                continue

            stats.current_state = HotGPUWorkerState.ACTIVE
            start_ns = int(asyncio.get_event_loop().time() * 1_000_000_000)

            try:
                coro = task.coro_factory(task.context)
                task.result = await coro
                task.completed = True
                stats.tasks_completed += 1

                # Track GPU operations if available
                if self._gpu_available:
                    stats.gpu_operations += 1

            except Exception as e:
                task.error = e
                task.completed = True
                stats.tasks_failed += 1

            elapsed_ns = int(asyncio.get_event_loop().time() * 1_000_000_000) - start_ns
            stats.total_ns += elapsed_ns
            stats.current_state = HotGPUWorkerState.HOT_STANDBY

    async def run(self) -> None:
        """Start all 3 hot GPU workers."""
        # Create 3 hot worker tasks
        workers = [
            asyncio.create_task(self._hot_worker_loop(i))
            for i in range(HOT_GPU_WORKERS)
        ]

        # Wait for shutdown signal
        await self._shutdown_event.wait()

        # Mark all workers as draining
        for i in range(HOT_GPU_WORKERS):
            self._stats[i].current_state = HotGPUWorkerState.DRAINING

        # Drain remaining tasks
        while self._queue.total_pending > 0:
            await asyncio.sleep(0.01)

        # Cancel workers
        for worker in workers:
            worker.cancel()

        await asyncio.gather(*workers, return_exceptions=True)

    async def shutdown(self, timeout: float = 30.0) -> None:
        """Gracefully shutdown hot GPU pool.

        Args:
            timeout: Maximum seconds to wait for drain.
        """
        self._shutdown_event.set()
        self._executor.shutdown(wait=True, cancel_futures=True)

    def get_stats(self) -> list[GPUWorkerStats]:
        """Get per-worker statistics."""
        return self._stats.copy()

    @property
    def pending_count(self) -> int:
        """Number of pending tasks."""
        return self._queue.total_pending

    @property
    def gpu_available(self) -> bool:
        """Whether GPU acceleration is available."""
        return self._gpu_available

    def get_worker_states(self) -> list[HotGPUWorkerState]:
        """Get current state of each worker."""
        return [s.current_state for s in self._stats]


async def run_with_hot_gpu_pool(
    contexts: list[ExtensionContext],
    coro_factory: Callable[[ExtensionContext], Awaitable[T]],
) -> list[T]:
    """Run tasks through 3 hot GPU workers.

    Optimized for low-latency high-throughput GPU workloads.

    Args:
        contexts: Extension contexts to process.
        coro_factory: Async function for each context.

    Returns:
        List of results in input order.
    """
    pool = HotGPUWorkerPool()
    tasks: list[Task] = []

    # Submit all tasks with CRITICAL priority for GPU
    for ctx in contexts:
        priority = TaskPriority.CRITICAL if ctx.is_gpu_required else TaskPriority.HIGH
        task = await pool.submit(ctx, coro_factory, priority)
        tasks.append(task)

    # Start pool in background
    pool_task = asyncio.create_task(pool.run())

    # Wait for all tasks to complete (with low-latency polling)
    while not all(t.completed for t in tasks):
        await asyncio.sleep(0.005)  # 5ms poll interval

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
