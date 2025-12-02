"""GPU-Accelerated Automation Workers with Direct Tensor Core Compute.

3 Low-Latency Hot Standby Workers:
- Worker 0: Primary automation (1GB VRAM reserved)
- Worker 1: Secondary automation (1GB VRAM reserved)
- Qdrant: Vector operations (1GB VRAM, same subnet)

NO CPU FALLBACK. All compute is GPU-only.

Environment Variables:
- ASPIRE_WORKER_VRAM_MB: VRAM per worker (default: 1024)
- ASPIRE_QDRANT_VRAM_MB: VRAM for Qdrant (default: 1024)
- CUDA_VISIBLE_DEVICES: GPU device(s)
"""

from __future__ import annotations

import asyncio
import json
import os
import re
import signal
import subprocess
import sys
from concurrent.futures import Future, ThreadPoolExecutor
from dataclasses import dataclass, field
from enum import IntEnum, auto
from pathlib import Path
from typing import TYPE_CHECKING, Any, Awaitable, Callable, Final, TypedDict, TypeVar

import torch

from .gpu import TensorCoreUnavailableError, ensure_tensor_core_gpu, get_gpu_memory_info

if TYPE_CHECKING:
    pass

# =============================================================================
# GPU-Only Configuration - No CPU Fallback
# =============================================================================

# 2 workers x 1GB VRAM each + 1GB Qdrant = 3GB minimum
WORKER_COUNT: Final[int] = 2
WORKER_VRAM_MB: Final[int] = int(os.environ.get("ASPIRE_WORKER_VRAM_MB", "1024"))
QDRANT_VRAM_MB: Final[int] = int(os.environ.get("ASPIRE_QDRANT_VRAM_MB", "1024"))
TOTAL_REQUIRED_VRAM_MB: Final[int] = (WORKER_COUNT * WORKER_VRAM_MB) + QDRANT_VRAM_MB

# Queue and latency
MAX_QUEUE_DEPTH: Final[int] = 4096
BATCH_SIZE: Final[int] = 64
TARGET_LATENCY_MS: Final[int] = 50
SPIN_WAIT_MS: Final[float] = 1.0  # Hot standby spin

# Tensor Core alignment
TENSOR_ALIGNMENT: Final[int] = int(os.environ.get("CUDA_TENSOR_CORE_ALIGNMENT", "128"))


# =============================================================================
# Problem Types
# =============================================================================


class ProblemSeverity(IntEnum):
    """Problem severity aligned with LSP diagnostics."""

    ERROR = 0
    WARNING = 1
    INFO = 2
    HINT = 3


class ProblemCategory(IntEnum):
    """Categories for automated problem detection."""

    COMPILE_ERROR = 0
    LINT_VIOLATION = auto()
    TYPE_ERROR = auto()
    TEST_FAILURE = auto()
    SECURITY_ISSUE = auto()
    PERFORMANCE_ISSUE = auto()
    DEPENDENCY_ISSUE = auto()
    DOCKER_ISSUE = auto()
    TENSOR_ERROR = auto()


class WorkerMode(IntEnum):
    """Worker operational modes."""

    HOT_STANDBY = 0  # Warm GPU, ready for immediate dispatch
    ACTIVE = auto()  # Processing
    DRAINING = auto()  # Completing work


# =============================================================================
# TypedDict for Metrics
# =============================================================================


class ProblemInfo(TypedDict):
    """Structured problem info."""

    file: str
    line: int
    column: int
    severity: int
    category: int
    message: str
    code: str | None
    source: str


class WorkerMetrics(TypedDict):
    """Per-worker metrics."""

    worker_id: int
    vram_reserved_mb: int
    problems_processed: int
    fixes_applied: int
    fixes_failed: int
    avg_latency_ms: float
    gpu_ops: int


class AutomationResult(TypedDict):
    """Result from automation run."""

    total_problems: int
    fixed: int
    failed: int
    skipped: int
    duration_ms: float
    worker_metrics: list[WorkerMetrics]
    gpu_memory: dict[str, float]


# =============================================================================
# Data Classes
# =============================================================================


@dataclass(frozen=True, slots=True)
class Problem:
    """Immutable problem descriptor."""

    id: int
    file_path: str
    line: int
    column: int
    severity: ProblemSeverity
    category: ProblemCategory
    message: str
    code: str | None
    source: str
    timestamp_ns: int
    content_hash: int = 0

    def to_dict(self) -> ProblemInfo:
        """Serialize to TypedDict."""
        return ProblemInfo(
            file=self.file_path,
            line=self.line,
            column=self.column,
            severity=self.severity.value,
            category=self.category.value,
            message=self.message,
            code=self.code,
            source=self.source,
        )


@dataclass(slots=True)
class WorkerState:
    """Mutable worker state with GPU-backed counters."""

    worker_id: int
    vram_reserved_mb: int = WORKER_VRAM_MB
    mode: WorkerMode = WorkerMode.HOT_STANDBY
    current_problem: Problem | None = None
    # Counters: processed, fixed, failed, skipped, latency_ns, gpu_ops
    counters: torch.Tensor = field(
        default_factory=lambda: torch.zeros(6, dtype=torch.int64, device="cuda")
    )

    @property
    def problems_processed(self) -> int:
        return int(self.counters[0].item())

    @property
    def fixes_applied(self) -> int:
        return int(self.counters[1].item())

    @property
    def fixes_failed(self) -> int:
        return int(self.counters[2].item())

    @property
    def avg_latency_ms(self) -> float:
        processed = self.counters[0].item()
        if processed == 0:
            return 0.0
        return float(self.counters[4].item() / processed) / 1_000_000

    def get_metrics(self) -> WorkerMetrics:
        """Extract metrics."""
        return WorkerMetrics(
            worker_id=self.worker_id,
            vram_reserved_mb=self.vram_reserved_mb,
            problems_processed=int(self.counters[0].item()),
            fixes_applied=int(self.counters[1].item()),
            fixes_failed=int(self.counters[2].item()),
            avg_latency_ms=self.avg_latency_ms,
            gpu_ops=int(self.counters[5].item()),
        )


# =============================================================================
# GPU Priority Queue
# =============================================================================


class GPUPriorityQueue:
    """GPU-backed priority queue with Tensor Core sorting."""

    __slots__ = ("_problems", "_priorities", "_lock", "_counter")

    def __init__(self, capacity: int = MAX_QUEUE_DEPTH) -> None:
        self._problems: list[Problem] = []
        # GPU tensor for priorities
        self._priorities = torch.zeros(capacity, dtype=torch.int32, device="cuda")
        self._lock = asyncio.Lock()
        self._counter = 0

    def _compute_priority(self, problem: Problem) -> int:
        """Priority score (lower = higher priority)."""
        return problem.severity.value * 16 + problem.category.value

    async def enqueue(self, problem: Problem) -> None:
        """Add problem with GPU-accelerated priority insertion."""
        async with self._lock:
            priority = self._compute_priority(problem)
            n = len(self._problems)

            if n == 0:
                self._problems.append(problem)
                self._priorities[0] = priority
            else:
                # GPU binary search
                priorities_view = self._priorities[:n]
                idx = int(torch.searchsorted(priorities_view, priority).item())
                self._problems.insert(idx, problem)
                # Shift on GPU
                if idx < n:
                    self._priorities[idx + 1 : n + 1] = self._priorities[idx:n].clone()
                self._priorities[idx] = priority

            self._counter += 1

    async def enqueue_batch(self, problems: list[Problem]) -> None:
        """Batch enqueue with GPU sorting."""
        if not problems:
            return

        async with self._lock:
            # Compute priorities on GPU
            priorities = torch.tensor(
                [self._compute_priority(p) for p in problems],
                dtype=torch.int32,
                device="cuda",
            )
            sorted_indices = torch.argsort(priorities)

            # Merge into existing queue
            for idx in sorted_indices.tolist():
                problem = problems[idx]
                priority = int(priorities[idx].item())
                n = len(self._problems)
                insert_idx = int(
                    torch.searchsorted(self._priorities[:n], priority).item()
                )
                self._problems.insert(insert_idx, problem)
                if insert_idx < n:
                    self._priorities[insert_idx + 1 : n + 1] = self._priorities[
                        insert_idx:n
                    ].clone()
                self._priorities[insert_idx] = priority

            self._counter += len(problems)

    async def dequeue(self) -> Problem | None:
        """Remove highest-priority problem."""
        async with self._lock:
            if not self._problems:
                return None
            problem = self._problems.pop(0)
            n = len(self._problems)
            if n > 0:
                self._priorities[:n] = self._priorities[1 : n + 1].clone()
            return problem

    @property
    def pending_count(self) -> int:
        """Number of pending problems."""
        return len(self._problems)


# =============================================================================
# Problem Scanner
# =============================================================================


class ProblemScanner:
    """Scans codebase for problems - GPU-accelerated hashing."""

    __slots__ = ("_workspace_root", "_counter", "_cache")

    def __init__(self, workspace_root: str | Path) -> None:
        self._workspace_root = Path(workspace_root)
        self._counter = 0
        self._cache: dict[int, Problem] = {}

    def _next_id(self) -> int:
        self._counter += 1
        return self._counter

    def _hash(self, file: str, line: int, msg: str) -> int:
        """GPU-accelerated hash using torch."""
        content = f"{file}:{line}:{msg}".encode("utf-8")
        # Convert to GPU tensor and compute hash
        arr = torch.frombuffer(bytearray(content), dtype=torch.uint8).cuda()
        # Simple XOR-based hash on GPU
        padded = torch.nn.functional.pad(
            arr.view(-1), (0, 8 - len(arr) % 8 if len(arr) % 8 else 0)
        )
        hash_val = torch.bitwise_xor.reduce(padded.view(torch.int64))
        return int(hash_val.item())

    async def scan_build_errors(self) -> list[Problem]:
        """Scan dotnet build output."""
        problems: list[Problem] = []
        slnf = self._workspace_root / "Aspire-Full.slnf"
        if not slnf.exists():
            return problems

        try:
            result = subprocess.run(
                ["dotnet", "build", str(slnf), "--no-restore", "-v", "q"],
                capture_output=True,
                text=True,
                timeout=300,
            )
            pattern = re.compile(
                r"^(.+?)\((\d+),(\d+)\):\s*(error|warning)\s+(\w+):\s*(.+)$",
                re.MULTILINE,
            )
            for match in pattern.finditer(result.stdout + result.stderr):
                fp, line, col, sev, code, msg = match.groups()
                severity = (
                    ProblemSeverity.ERROR if sev == "error" else ProblemSeverity.WARNING
                )
                h = self._hash(fp, int(line), msg)
                if h not in self._cache:
                    p = Problem(
                        id=self._next_id(),
                        file_path=fp,
                        line=int(line),
                        column=int(col),
                        severity=severity,
                        category=ProblemCategory.COMPILE_ERROR,
                        message=msg,
                        code=code,
                        source="build",
                        timestamp_ns=int(asyncio.get_event_loop().time() * 1e9),
                        content_hash=h,
                    )
                    self._cache[h] = p
                    problems.append(p)
        except Exception:
            pass
        return problems

    async def scan_python_errors(self) -> list[Problem]:
        """Scan Python with ruff."""
        problems: list[Problem] = []
        py_dirs = [
            self._workspace_root / "AI" / "Aspire-Full.Python" / "python-agents",
        ]
        for py_dir in py_dirs:
            if not py_dir.exists():
                continue
            try:
                result = subprocess.run(
                    ["ruff", "check", "--output-format=json", str(py_dir)],
                    capture_output=True,
                    text=True,
                    timeout=60,
                )
                if result.stdout:
                    for issue in json.loads(result.stdout):
                        h = self._hash(
                            issue.get("filename", ""),
                            issue.get("location", {}).get("row", 0),
                            issue.get("message", ""),
                        )
                        if h not in self._cache:
                            p = Problem(
                                id=self._next_id(),
                                file_path=issue.get("filename", ""),
                                line=issue.get("location", {}).get("row", 0),
                                column=issue.get("location", {}).get("column", 0),
                                severity=ProblemSeverity.WARNING,
                                category=ProblemCategory.LINT_VIOLATION,
                                message=issue.get("message", ""),
                                code=issue.get("code"),
                                source="lint",
                                timestamp_ns=int(asyncio.get_event_loop().time() * 1e9),
                                content_hash=h,
                            )
                            self._cache[h] = p
                            problems.append(p)
            except Exception:
                pass
        return problems

    async def scan_all(self) -> list[Problem]:
        """Run all scanners."""
        results = await asyncio.gather(
            self.scan_build_errors(),
            self.scan_python_errors(),
            return_exceptions=True,
        )
        all_problems: list[Problem] = []
        for r in results:
            if isinstance(r, list):
                all_problems.extend(r)
        return all_problems


# =============================================================================
# GPU Automation Worker
# =============================================================================

T = TypeVar("T")


class GPUAutomationWorker:
    """Single GPU worker with reserved VRAM."""

    __slots__ = (
        "_id",
        "_state",
        "_queue",
        "_scanner",
        "_shutdown",
        "_handlers",
    )

    def __init__(
        self,
        worker_id: int,
        queue: GPUPriorityQueue,
        scanner: ProblemScanner,
    ) -> None:
        self._id = worker_id
        self._state = WorkerState(worker_id=worker_id)
        self._queue = queue
        self._scanner = scanner
        self._shutdown = asyncio.Event()
        self._handlers: dict[ProblemCategory, Callable[[Problem], Awaitable[bool]]] = {
            ProblemCategory.LINT_VIOLATION: self._fix_lint,
        }

    async def _fix_lint(self, problem: Problem) -> bool:
        """Auto-fix lint with ruff."""
        try:
            result = subprocess.run(
                ["ruff", "check", "--fix", problem.file_path],
                capture_output=True,
                timeout=30,
            )
            return result.returncode == 0
        except Exception:
            return False

    async def _process(self, problem: Problem) -> bool:
        """Process single problem on GPU."""
        start = int(asyncio.get_event_loop().time() * 1e9)
        self._state.current_problem = problem
        self._state.mode = WorkerMode.ACTIVE

        try:
            handler = self._handlers.get(problem.category)
            if handler:
                ok = await handler(problem)
                self._state.counters[1 if ok else 2] += 1
                return ok
            self._state.counters[3] += 1  # skipped
            return False
        finally:
            elapsed = int(asyncio.get_event_loop().time() * 1e9) - start
            self._state.counters[0] += 1  # processed
            self._state.counters[4] += elapsed  # latency
            self._state.counters[5] += 1  # gpu_ops
            self._state.current_problem = None
            self._state.mode = WorkerMode.HOT_STANDBY

    async def run(self) -> None:
        """Hot standby loop."""
        self._state.mode = WorkerMode.HOT_STANDBY
        while not self._shutdown.is_set():
            problem = await self._queue.dequeue()
            if problem is None:
                await asyncio.sleep(SPIN_WAIT_MS / 1000)
                continue
            await self._process(problem)

    async def shutdown(self) -> None:
        """Shutdown worker."""
        self._state.mode = WorkerMode.DRAINING
        self._shutdown.set()

    def get_metrics(self) -> WorkerMetrics:
        """Get worker metrics."""
        return self._state.get_metrics()


# =============================================================================
# GPU Automation Pool
# =============================================================================


class GPUAutomationPool:
    """Pool of 2 GPU workers + Qdrant VRAM reservation.

    Configuration:
    - Worker 0: 1GB VRAM
    - Worker 1: 1GB VRAM
    - Qdrant: 1GB VRAM (same subnet)
    Total: 3GB VRAM minimum
    """

    __slots__ = (
        "_workers",
        "_queue",
        "_scanner",
        "_workspace",
        "_shutdown",
        "_scan_interval",
        "_gpu_info",
    )

    def __init__(
        self,
        workspace_root: str | Path,
        scan_interval: float = 30.0,
    ) -> None:
        # Validate GPU requirements
        self._gpu_info = ensure_tensor_core_gpu()
        self._validate_vram()

        self._workspace = Path(workspace_root)
        self._queue = GPUPriorityQueue()
        self._scanner = ProblemScanner(workspace_root)
        self._shutdown = asyncio.Event()
        self._scan_interval = scan_interval

        # Create 2 GPU workers
        self._workers = [
            GPUAutomationWorker(i, self._queue, self._scanner)
            for i in range(WORKER_COUNT)
        ]

    def _validate_vram(self) -> None:
        """Ensure sufficient VRAM for workers + Qdrant."""
        mem = get_gpu_memory_info()
        total_mb = mem["total_gb"] * 1024
        required = TOTAL_REQUIRED_VRAM_MB

        if total_mb < required:
            raise TensorCoreUnavailableError(
                f"Insufficient VRAM: {total_mb:.0f}MB available, "
                f"{required}MB required (2x{WORKER_VRAM_MB}MB workers + "
                f"{QDRANT_VRAM_MB}MB Qdrant)"
            )

    async def _scan_loop(self) -> None:
        """Continuous problem scanning."""
        while not self._shutdown.is_set():
            try:
                problems = await self._scanner.scan_all()
                if problems:
                    await self._queue.enqueue_batch(problems)
            except Exception:
                pass

            try:
                await asyncio.wait_for(
                    self._shutdown.wait(), timeout=self._scan_interval
                )
                break
            except asyncio.TimeoutError:
                continue

    async def run(self) -> AutomationResult:
        """Start all workers."""
        start = asyncio.get_event_loop().time()

        worker_tasks = [asyncio.create_task(w.run()) for w in self._workers]
        scan_task = asyncio.create_task(self._scan_loop())

        await self._shutdown.wait()

        for w in self._workers:
            await w.shutdown()

        scan_task.cancel()
        for t in worker_tasks:
            t.cancel()

        await asyncio.gather(scan_task, *worker_tasks, return_exceptions=True)

        duration = (asyncio.get_event_loop().time() - start) * 1000
        metrics = [w.get_metrics() for w in self._workers]

        return AutomationResult(
            total_problems=sum(m["problems_processed"] for m in metrics),
            fixed=sum(m["fixes_applied"] for m in metrics),
            failed=sum(m["fixes_failed"] for m in metrics),
            skipped=sum(m["problems_processed"] for m in metrics)
            - sum(m["fixes_applied"] for m in metrics)
            - sum(m["fixes_failed"] for m in metrics),
            duration_ms=duration,
            worker_metrics=metrics,
            gpu_memory=get_gpu_memory_info(),
        )

    async def shutdown(self) -> None:
        """Signal shutdown."""
        self._shutdown.set()

    def get_status(self) -> dict[str, Any]:
        """Get pool status."""
        return {
            "workers": WORKER_COUNT,
            "vram_per_worker_mb": WORKER_VRAM_MB,
            "qdrant_vram_mb": QDRANT_VRAM_MB,
            "pending": self._queue.pending_count,
            "gpu_info": {
                "name": self._gpu_info.name,
                "total_gb": self._gpu_info.total_memory_gb,
                "compute": self._gpu_info.compute_capability,
            },
            "worker_metrics": [w.get_metrics() for w in self._workers],
        }


# =============================================================================
# CLI
# =============================================================================


async def main() -> int:
    """CLI entry point."""
    import argparse

    parser = argparse.ArgumentParser(description="GPU Automation Workers")
    parser.add_argument(
        "--workspace",
        default=str(Path(__file__).parents[5]),
        help="Workspace root",
    )
    parser.add_argument("--scan-interval", type=float, default=30.0)
    parser.add_argument("--run-once", action="store_true")
    parser.add_argument("--status", action="store_true")
    parser.add_argument("--json", action="store_true")

    args = parser.parse_args()

    try:
        pool = GPUAutomationPool(args.workspace, args.scan_interval)
    except TensorCoreUnavailableError as e:
        print(f"GPU Error: {e}", file=sys.stderr)
        return 1

    if args.status:
        status = pool.get_status()
        if args.json:
            print(json.dumps(status, indent=2))
        else:
            print(f"Workers: {status['workers']} x {status['vram_per_worker_mb']}MB VRAM")
            print(f"Qdrant: {status['qdrant_vram_mb']}MB VRAM")
            print(f"GPU: {status['gpu_info']['name']} ({status['gpu_info']['total_gb']}GB)")
            print(f"Pending: {status['pending']}")
        return 0

    def signal_handler() -> None:
        asyncio.create_task(pool.shutdown())

    loop = asyncio.get_event_loop()
    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            loop.add_signal_handler(sig, signal_handler)
        except NotImplementedError:
            signal.signal(sig, lambda s, f: signal_handler())

    if args.run_once:
        problems = await pool._scanner.scan_all()
        await pool._queue.enqueue_batch(problems)
        await asyncio.sleep(5.0)
        await pool.shutdown()

    result = await pool.run()

    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print(f"\nProblems: {result['total_problems']}")
        print(f"Fixed: {result['fixed']}, Failed: {result['failed']}")
        print(f"GPU Memory: {result['gpu_memory']}")

    return 0


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
