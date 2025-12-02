"""Python profiler vendor abstractions with native tensor compute integration.

Provides protocol definitions for cProfile and pstats modules,
enabling type checking for profiling code with GPU compute metrics.

The profiler modules provide:
- cProfile: Deterministic profiling with low overhead
- pstats: Statistics analysis and reporting
- TensorComputeProfiler: GPU-aware profiling with native metrics
- CodeQualityAnalyzer: Static analysis integration

Use for:
- Performance bottleneck identification
- Call graph analysis
- Optimization verification
- GPU compute efficiency analysis
- Code quality metrics collection
"""

from __future__ import annotations

import time
from collections.abc import Callable, Generator, Mapping
from contextlib import contextmanager
from dataclasses import dataclass, field
from types import TracebackType
from typing import (
    Any,
    Final,
    Protocol,
    Self,
    TypeVar,
    cast,
    runtime_checkable,
)

from ._enums import ProfilerSortKey, TorchDeviceType, TorchDtypeEnum

# Re-export for backwards compatibility
SortKey = ProfilerSortKey

# ============================================================================
# Type Variables
# ============================================================================

T = TypeVar("T")
P = TypeVar("P", bound="ProfileProtocol")
S = TypeVar("S", bound="StatsProtocol")


# ============================================================================
# Profile Data Classes
# ============================================================================


@dataclass(slots=True)
class FunctionProfile:
    """Profile statistics for a single function.

    Contains timing and call count information.
    """

    ncalls: str
    """Number of calls (format: "total/primitive" or "total")."""

    tottime: float
    """Total time spent in the function (excluding callees)."""

    percall_tottime: float
    """Average time per call (tottime / ncalls)."""

    cumtime: float
    """Cumulative time (including callees)."""

    percall_cumtime: float
    """Average cumulative time per call."""

    file_name: str
    """Source file containing the function."""

    line_number: int
    """Line number where function is defined."""


@dataclass(slots=True)
class StatsProfile:
    """Aggregated profiling statistics.

    Contains overall timing and per-function profiles.
    """

    total_tt: float
    """Total time for all profiled functions."""

    func_profiles: dict[str, FunctionProfile]
    """Per-function profiles keyed by function identifier."""


@dataclass(slots=True)
class TensorMetrics:
    """Native tensor compute metrics.

    Mirrors the C# NativeTensorContext.TensorMetrics struct for
    cross-language profiling integration.
    """

    compute_time_ms: float = 0.0
    """Time spent in GPU compute operations."""

    memory_usage_mb: float = 0.0
    """GPU memory usage in megabytes."""

    active_kernels: int = 0
    """Number of active CUDA kernels."""

    gpu_utilization_percent: int = 0
    """GPU utilization percentage (0-100)."""

    total_flops: int = 0
    """Total floating-point operations performed."""

    hash_time_ms: float = 0.0
    """Time for tensor hashing operations."""

    compress_time_ms: float = 0.0
    """Time for tensor compression."""

    transfer_time_ms: float = 0.0
    """Time for host-device memory transfers."""


@dataclass(slots=True)
class ComputeProfile:
    """Combined Python + GPU compute profile.

    Integrates cProfile statistics with native tensor metrics.
    """

    python_stats: StatsProfile | None = None
    """Python profiling statistics."""

    tensor_metrics: TensorMetrics = field(default_factory=TensorMetrics)
    """GPU compute metrics from native context."""

    wall_time_ms: float = 0.0
    """Wall-clock time for the profiled operation."""

    device_type: TorchDeviceType = TorchDeviceType.CUDA
    """Device type used for compute."""

    dtype: TorchDtypeEnum = TorchDtypeEnum.FLOAT32
    """Data type used in tensor operations."""


@dataclass(slots=True)
class CodeQualityMetrics:
    """Code quality metrics from static analysis.

    Collected via profiling integration with linters/analyzers.
    """

    complexity_score: float = 0.0
    """Cyclomatic complexity score."""

    maintainability_index: float = 100.0
    """Maintainability index (0-100, higher is better)."""

    lines_of_code: int = 0
    """Total lines of code analyzed."""

    function_count: int = 0
    """Number of functions profiled."""

    hotspot_count: int = 0
    """Number of performance hotspots detected."""

    coverage_percent: float = 0.0
    """Code coverage percentage from profiling."""


# ============================================================================
# Profile Protocol (cProfile.Profile)
# ============================================================================


@runtime_checkable
class ProfileProtocol(Protocol):
    """Protocol for cProfile.Profile.

    Provides deterministic profiling of Python code.
    Can be used as a context manager.

    Example:
        with Profile() as pr:
            my_function()
        pr.print_stats()
    """

    # Instance attributes
    stats: dict[
        tuple[str, int, str],
        tuple[int, int, int, int, dict[tuple[str, int, str], tuple[int, int, int, int]]],
    ]
    """Raw statistics dictionary.

    Keys: (filename, lineno, funcname)
    Values: (ncalls, totcalls, tottime, cumtime, callers)
    """

    def run(self: P, cmd: str) -> P:
        """Profile a command string.

        Args:
            cmd: Python code to execute and profile

        Returns:
            Self for method chaining

        Example:
            pr.run("my_function()")
        """
        ...

    def runcall(
        self,
        func: Callable[..., T],
        /,
        *args: Any,
        **kwargs: Any,
    ) -> T:
        """Profile a function call.

        Args:
            func: Function to call
            *args: Positional arguments
            **kwargs: Keyword arguments

        Returns:
            Function return value

        Example:
            result = pr.runcall(my_function, arg1, arg2)
        """
        ...

    def runctx(
        self: P,
        cmd: str,
        globals: dict[str, Any],
        locals: Mapping[str, Any],
    ) -> P:
        """Profile a command with custom namespace.

        Args:
            cmd: Python code to execute
            globals: Global namespace
            locals: Local namespace

        Returns:
            Self for method chaining
        """
        ...

    def enable(self) -> None:
        """Enable profiling.

        Starts collecting profiling data.
        """
        ...

    def disable(self) -> None:
        """Disable profiling.

        Stops collecting profiling data.
        """
        ...

    def create_stats(self) -> None:
        """Create stats from collected data.

        Populates the stats attribute.
        """
        ...

    def snapshot_stats(self) -> None:
        """Snapshot current stats.

        Creates a point-in-time snapshot of profiling data.
        """
        ...

    def dump_stats(self, file: str | bytes) -> None:
        """Dump stats to a file.

        Args:
            file: Path to output file

        Note:
            Output can be loaded by pstats.Stats
        """
        ...

    def print_stats(self, sort: str | int = -1) -> None:
        """Print stats to stdout.

        Args:
            sort: Sort key (string or legacy integer)
        """
        ...

    def __enter__(self: P) -> P:
        """Enter profiling context.

        Returns:
            Self with profiling enabled
        """
        ...

    def __exit__(
        self,
        __exc_type: type[BaseException] | None,
        __exc_val: BaseException | None,
        __exc_tb: TracebackType | None,
        /,
    ) -> None:
        """Exit profiling context.

        Disables profiling on exit.
        """
        ...


# ============================================================================
# Stats Protocol (pstats.Stats)
# ============================================================================


@runtime_checkable
class StatsProtocol(Protocol):
    """Protocol for pstats.Stats.

    Analyzes and reports profiling statistics.

    Example:
        stats = Stats("profile.pstats")
        stats.sort_stats(SortKey.CUMULATIVE)
        stats.print_stats(10)
    """

    def add(self: S, *args: str | ProfileProtocol | S) -> S:
        """Add additional profile data.

        Args:
            *args: Additional profile sources

        Returns:
            Self for method chaining
        """
        ...

    def sort_stats(self: S, *keys: str | SortKey | int) -> S:
        """Sort statistics by given keys.

        Args:
            *keys: Sort keys (SortKey enum, string, or int)

        Returns:
            Self for method chaining

        Example:
            stats.sort_stats(SortKey.CUMULATIVE, SortKey.NAME)
        """
        ...

    def reverse_order(self: S) -> S:
        """Reverse the current sort order.

        Returns:
            Self for method chaining
        """
        ...

    def strip_dirs(self: S) -> S:
        """Remove directory paths from filenames.

        Makes output more compact.

        Returns:
            Self for method chaining
        """
        ...

    def print_stats(self: S, *amount: str | int | float) -> S:
        """Print statistics.

        Args:
            *amount: Limit output by count, percentage, or regex

        Returns:
            Self for method chaining

        Example:
            stats.print_stats(10)  # Top 10
            stats.print_stats(0.1)  # Top 10%
            stats.print_stats("foo")  # Functions matching "foo"
        """
        ...

    def print_callers(self: S, *amount: str | int | float) -> S:
        """Print callers of functions.

        Shows which functions called each function.

        Args:
            *amount: Limit output

        Returns:
            Self for method chaining
        """
        ...

    def print_callees(self: S, *amount: str | int | float) -> S:
        """Print callees of functions.

        Shows which functions each function calls.

        Args:
            *amount: Limit output

        Returns:
            Self for method chaining
        """
        ...

    def dump_stats(self, filename: str | bytes) -> None:
        """Dump stats to a file.

        Args:
            filename: Output file path
        """
        ...

    def get_stats_profile(self) -> StatsProfile:
        """Get structured profile data.

        Returns:
            StatsProfile with function-level data
        """
        ...


# ============================================================================
# Tensor Compute Profiler Protocol
# ============================================================================


@runtime_checkable
class TensorComputeProfilerProtocol(Protocol):
    """Protocol for GPU-aware profiler with native tensor metrics.

    Integrates Python profiling with CUDA/native compute metrics.
    """

    def start(self) -> None:
        """Start profiling Python and GPU operations."""
        ...

    def stop(self) -> ComputeProfile:
        """Stop profiling and return combined metrics.

        Returns:
            ComputeProfile with Python stats and GPU metrics.
        """
        ...

    def profile_function(
        self,
        func: Callable[..., T],
        *args: Any,
        **kwargs: Any,
    ) -> tuple[T, ComputeProfile]:
        """Profile a single function call.

        Args:
            func: Function to profile
            *args: Positional arguments
            **kwargs: Keyword arguments

        Returns:
            Tuple of (function result, profile data)
        """
        ...


# ============================================================================
# Code Quality Analyzer Protocol
# ============================================================================


@runtime_checkable
class CodeQualityAnalyzerProtocol(Protocol):
    """Protocol for code quality analysis integration.

    Combines profiling data with static analysis for quality metrics.
    """

    def analyze_file(self, filepath: str) -> CodeQualityMetrics:
        """Analyze a single file for code quality.

        Args:
            filepath: Path to Python file

        Returns:
            Code quality metrics for the file
        """
        ...

    def analyze_profile(
        self,
        profile: StatsProfile,
    ) -> CodeQualityMetrics:
        """Analyze profiling data for quality metrics.

        Args:
            profile: Profiling statistics

        Returns:
            Code quality metrics derived from profiling
        """
        ...


# ============================================================================
# Tensor Compute Profiler Implementation
# ============================================================================


class TensorComputeProfiler:
    """GPU-aware profiler integrating Python cProfile with native metrics.

    Provides unified profiling for code running on both CPU and GPU,
    collecting metrics from both the Python profiler and CUDA runtime.

    Example:
        profiler = TensorComputeProfiler()
        profiler.start()
        # ... run GPU operations ...
        profile = profiler.stop()
        print(f"GPU time: {profile.tensor_metrics.compute_time_ms}ms")
    """

    __slots__ = (
        "_profile",
        "_start_time",
        "_device_type",
        "_dtype",
        "_is_profiling",
    )

    def __init__(
        self,
        device_type: TorchDeviceType = TorchDeviceType.CUDA,
        dtype: TorchDtypeEnum = TorchDtypeEnum.FLOAT32,
    ) -> None:
        """Initialize the tensor compute profiler.

        Args:
            device_type: Device type for compute operations.
            dtype: Data type for tensor operations.
        """
        super().__init__()
        self._profile: ProfileProtocol | None = None
        self._start_time: float = 0.0
        self._device_type = device_type
        self._dtype = dtype
        self._is_profiling = False

    def start(self) -> None:
        """Start profiling Python and GPU operations."""
        if self._is_profiling:
            return

        self._profile = create_profile()
        self._start_time = time.perf_counter()
        self._profile.enable()
        self._is_profiling = True

    def stop(self) -> ComputeProfile:
        """Stop profiling and return combined metrics.

        Returns:
            ComputeProfile with Python stats and GPU metrics.
        """
        if not self._is_profiling or self._profile is None:
            return ComputeProfile()

        self._profile.disable()
        wall_time = (time.perf_counter() - self._start_time) * 1000  # ms
        self._is_profiling = False

        # Create stats from profile
        stats = create_stats(self._profile)
        stats.sort_stats(SortKey.CUMULATIVE)

        # Extract StatsProfile
        python_stats = _extract_stats_profile(self._profile)

        # Collect tensor metrics (would integrate with native context)
        tensor_metrics = _collect_tensor_metrics()

        return ComputeProfile(
            python_stats=python_stats,
            tensor_metrics=tensor_metrics,
            wall_time_ms=wall_time,
            device_type=self._device_type,
            dtype=self._dtype,
        )

    def profile_function(
        self,
        func: Callable[..., T],
        *args: Any,
        **kwargs: Any,
    ) -> tuple[T, ComputeProfile]:
        """Profile a single function call.

        Args:
            func: Function to profile
            *args: Positional arguments
            **kwargs: Keyword arguments

        Returns:
            Tuple of (function result, profile data)
        """
        self.start()
        try:
            result = func(*args, **kwargs)
        finally:
            profile = self.stop()
        return result, profile

    def __enter__(self) -> Self:
        """Enter profiling context."""
        self.start()
        return self

    def __exit__(
        self,
        __exc_type: type[BaseException] | None,
        __exc_val: BaseException | None,
        __exc_tb: TracebackType | None,
        /,
    ) -> None:
        """Exit profiling context."""
        self.stop()


# ============================================================================
# Code Quality Analyzer Implementation
# ============================================================================


class CodeQualityAnalyzer:
    """Analyzer combining profiling with static analysis for quality metrics.

    Identifies performance hotspots and code quality issues from
    profiling data and optional static analysis integration.
    """

    __slots__ = ("_hotspot_threshold_percent",)

    def __init__(self, hotspot_threshold_percent: float = 10.0) -> None:
        """Initialize the code quality analyzer.

        Args:
            hotspot_threshold_percent: Threshold for marking a function
                as a hotspot (percentage of total time).
        """
        super().__init__()
        self._hotspot_threshold_percent = hotspot_threshold_percent

    def analyze_file(self, filepath: str) -> CodeQualityMetrics:
        """Analyze a single file for code quality.

        Args:
            filepath: Path to Python file

        Returns:
            Code quality metrics for the file
        """
        import ast

        with open(filepath, encoding="utf-8") as f:
            source = f.read()

        tree = ast.parse(source)
        lines = source.count("\n") + 1
        functions = sum(
            1 for node in ast.walk(tree) if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef))
        )

        # Estimate complexity (simplified)
        complexity = _estimate_complexity(tree)

        return CodeQualityMetrics(
            complexity_score=complexity,
            maintainability_index=max(0.0, 100.0 - complexity * 2),
            lines_of_code=lines,
            function_count=functions,
            hotspot_count=0,
            coverage_percent=0.0,
        )

    def analyze_profile(
        self,
        profile: StatsProfile,
    ) -> CodeQualityMetrics:
        """Analyze profiling data for quality metrics.

        Args:
            profile: Profiling statistics

        Returns:
            Code quality metrics derived from profiling
        """
        hotspots = 0
        if profile.total_tt > 0:
            threshold = profile.total_tt * (self._hotspot_threshold_percent / 100.0)
            for func_profile in profile.func_profiles.values():
                if func_profile.tottime >= threshold:
                    hotspots += 1

        return CodeQualityMetrics(
            complexity_score=0.0,
            maintainability_index=100.0,
            lines_of_code=0,
            function_count=len(profile.func_profiles),
            hotspot_count=hotspots,
            coverage_percent=0.0,
        )


# ============================================================================
# Helper Functions
# ============================================================================


def _extract_stats_profile(profile: ProfileProtocol) -> StatsProfile:
    """Extract StatsProfile from cProfile.Profile.

    Args:
        profile: Profile instance with stats

    Returns:
        Structured StatsProfile
    """
    profile.create_stats()
    func_profiles: dict[str, FunctionProfile] = {}
    total_time = 0.0

    for (filename, lineno, funcname), (ncalls, totcalls, tottime, cumtime, _) in profile.stats.items():
        key = f"{filename}:{lineno}({funcname})"
        calls_str = str(ncalls) if ncalls == totcalls else f"{totcalls}/{ncalls}"
        func_profiles[key] = FunctionProfile(
            ncalls=calls_str,
            tottime=tottime,
            percall_tottime=tottime / ncalls if ncalls > 0 else 0.0,
            cumtime=cumtime,
            percall_cumtime=cumtime / ncalls if ncalls > 0 else 0.0,
            file_name=filename,
            line_number=lineno,
        )
        total_time += tottime

    return StatsProfile(total_tt=total_time, func_profiles=func_profiles)


def _collect_tensor_metrics() -> TensorMetrics:
    """Collect metrics from native tensor context.

    Returns:
        TensorMetrics from GPU compute.
    """
    # This would integrate with the native NativeTensorContext
    # For now, return empty metrics that would be populated by actual GPU calls
    return TensorMetrics()


def _estimate_complexity(tree: Any) -> float:
    """Estimate cyclomatic complexity from AST.

    Args:
        tree: Parsed AST

    Returns:
        Estimated complexity score
    """
    import ast

    complexity = 1.0  # Base complexity

    for node in ast.walk(tree):
        # Decision points increase complexity
        if isinstance(node, (ast.If, ast.While, ast.For, ast.AsyncFor)):
            complexity += 1.0
        elif isinstance(node, ast.BoolOp):
            complexity += len(node.values) - 1
        elif isinstance(node, (ast.ExceptHandler, ast.With, ast.AsyncWith)):
            complexity += 0.5
        elif isinstance(node, ast.Try):
            complexity += 0.5
        elif isinstance(node, (ast.ListComp, ast.SetComp, ast.DictComp, ast.GeneratorExp)):
            complexity += 1.0

    return complexity


# ============================================================================
# Convenience Functions
# ============================================================================


def run(
    statement: str,
    filename: str | None = None,
    sort: str | int = -1,
) -> None:
    """Profile a statement and optionally save results.

    Args:
        statement: Python code to profile
        filename: Optional output file
        sort: Sort key for printing
    """
    profile = create_profile()
    profile.run(statement)
    if filename:
        profile.dump_stats(filename)
    else:
        profile.print_stats(sort)


def runctx(
    statement: str,
    globals: dict[str, Any],
    locals: Mapping[str, Any],
    filename: str | None = None,
    sort: str | int = -1,
) -> None:
    """Profile a statement in a custom namespace.

    Args:
        statement: Python code to profile
        globals: Global namespace
        locals: Local namespace
        filename: Optional output file
        sort: Sort key for printing
    """
    profile = create_profile()
    profile.runctx(statement, globals, locals)
    if filename:
        profile.dump_stats(filename)
    else:
        profile.print_stats(sort)


@contextmanager
def profile_context(
    device_type: TorchDeviceType = TorchDeviceType.CUDA,
    dtype: TorchDtypeEnum = TorchDtypeEnum.FLOAT32,
) -> Generator[TensorComputeProfiler, None, None]:
    """Context manager for GPU-aware profiling.

    Args:
        device_type: Device type for compute
        dtype: Data type for tensor ops

    Yields:
        TensorComputeProfiler instance

    Example:
        with profile_context() as profiler:
            # GPU operations here
            pass
        profile = profiler.stop()
    """
    profiler = TensorComputeProfiler(device_type=device_type, dtype=dtype)
    profiler.start()
    try:
        yield profiler
    finally:
        profiler.stop()


# ============================================================================
# Factory Functions
# ============================================================================


def create_profile() -> ProfileProtocol:
    """Create a new profiler instance.

    Returns:
        cProfile.Profile instance
    """
    from cProfile import Profile

    return Profile()


def create_stats(
    *args: str | ProfileProtocol,
    stream: Any = None,
) -> StatsProtocol:
    """Create a Stats analyzer.

    Args:
        *args: Profile files or Profile objects
        stream: Output stream

    Returns:
        pstats.Stats instance wrapped as StatsProtocol
    """
    from cProfile import Profile as CProfile
    from pstats import Stats

    # Convert ProfileProtocol to actual Profile for pstats.Stats
    converted_args: list[str | CProfile] = []
    for arg in args:
        if isinstance(arg, str):
            converted_args.append(arg)
        elif hasattr(arg, "getstats"):
            # ProfileProtocol instances are structurally compatible with cProfile.Profile
            converted_args.append(cast(CProfile, arg))

    stats_instance = Stats(*converted_args, stream=stream)
    # pstats.Stats is structurally compatible with StatsProtocol
    # cast is explicit type assertion, not suppression
    return cast(StatsProtocol, stats_instance)


def create_tensor_profiler(
    device_type: TorchDeviceType = TorchDeviceType.CUDA,
    dtype: TorchDtypeEnum = TorchDtypeEnum.FLOAT32,
) -> TensorComputeProfiler:
    """Create a GPU-aware tensor compute profiler.

    Args:
        device_type: Device type for compute operations.
        dtype: Data type for tensor operations.

    Returns:
        TensorComputeProfiler instance
    """
    return TensorComputeProfiler(device_type=device_type, dtype=dtype)


def create_quality_analyzer(
    hotspot_threshold_percent: float = 10.0,
) -> CodeQualityAnalyzer:
    """Create a code quality analyzer.

    Args:
        hotspot_threshold_percent: Threshold for marking hotspots.

    Returns:
        CodeQualityAnalyzer instance
    """
    return CodeQualityAnalyzer(hotspot_threshold_percent=hotspot_threshold_percent)


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Enums (re-exported)
    "SortKey",
    # Data Classes
    "FunctionProfile",
    "StatsProfile",
    "TensorMetrics",
    "ComputeProfile",
    "CodeQualityMetrics",
    # Protocols
    "ProfileProtocol",
    "StatsProtocol",
    "TensorComputeProfilerProtocol",
    "CodeQualityAnalyzerProtocol",
    # Implementations
    "TensorComputeProfiler",
    "CodeQualityAnalyzer",
    # Convenience Functions
    "run",
    "runctx",
    "profile_context",
    # Factory Functions
    "create_profile",
    "create_stats",
    "create_tensor_profiler",
    "create_quality_analyzer",
]
