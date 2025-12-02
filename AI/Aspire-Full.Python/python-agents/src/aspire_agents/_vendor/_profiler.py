"""Python profiler vendor abstractions.

Provides protocol definitions for cProfile and pstats modules,
enabling type checking for profiling code.

The profiler modules provide:
- cProfile: Deterministic profiling with low overhead
- pstats: Statistics analysis and reporting

Use for:
- Performance bottleneck identification
- Call graph analysis
- Optimization verification
"""

from __future__ import annotations

from collections.abc import Callable, Mapping
from dataclasses import dataclass
from types import TracebackType
from typing import (
    Any,
    Final,
    Protocol,
    TypeVar,
    runtime_checkable,
)

from ._enums import ProfilerSortKey

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


@dataclass
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


@dataclass
class StatsProfile:
    """Aggregated profiling statistics.

    Contains overall timing and per-function profiles.
    """

    total_tt: float
    """Total time for all profiled functions."""

    func_profiles: dict[str, FunctionProfile]
    """Per-function profiles keyed by function identifier."""


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

    def __init__(  # pyright: ignore[reportMissingSuperCall]
        self,
        *args: str | ProfileProtocol | None,
        stream: Any = None,
    ) -> None:
        """Initialize Stats from profile data.

        Args:
            *args: Profile files, Profile objects, or other Stats
            stream: Output stream (default: stdout)
        """
        ...

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
    ...


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
    ...


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
        pstats.Stats instance
    """
    from pstats import Stats

    return Stats(*args, stream=stream)  # type: ignore[arg-type]


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Enums
    "SortKey",
    # Data Classes
    "FunctionProfile",
    "StatsProfile",
    # Protocols
    "ProfileProtocol",
    "StatsProtocol",
    # Convenience Functions
    "run",
    "runctx",
    # Factory Functions
    "create_profile",
    "create_stats",
]
