"""Pytest vendor abstractions.

Provides protocol definitions for the pytest testing framework,
enabling type-safe test development without requiring pytest installation.

Pytest is Python's de facto testing framework providing:
- Simple test discovery and execution
- Powerful fixture system for test setup/teardown
- Parametrized testing
- Plugin architecture for extensibility
- Rich assertion introspection
- Async test support via pytest-asyncio
"""

from __future__ import annotations

from collections.abc import Callable, Generator, Iterator, Sequence
from contextlib import contextmanager
from dataclasses import dataclass, field
from types import TracebackType
from typing import (
    Any,
    Final,
    Literal,
    ParamSpec,
    Protocol,
    TypeAlias,
    TypeVar,
    overload,
    runtime_checkable,
)

from ._enums import PytestExitCode, PytestOutcome

# Re-export for backwards compatibility
ExitCodeEnum = PytestExitCode
TestOutcome = PytestOutcome

# ============================================================================
# Type Variables
# ============================================================================

T = TypeVar("T")
T_co = TypeVar("T_co", covariant=True)
T_exc = TypeVar("T_exc", bound=BaseException, covariant=True)
P = ParamSpec("P")
R = TypeVar("R")

# ============================================================================
# Type Aliases
# ============================================================================

FixtureScope: TypeAlias = Literal["function", "class", "module", "package", "session"]
"""Pytest fixture scopes determining lifecycle."""

MarkName: TypeAlias = Literal[
    "skip",
    "skipif",
    "xfail",
    "parametrize",
    "usefixtures",
    "filterwarnings",
    "timeout",
    "asyncio",
    "slow",
    "gpu",
]
"""Common pytest mark names."""

ExitCode: TypeAlias = Literal[0, 1, 2, 3, 4, 5]
"""Pytest exit codes: 0=OK, 1=TESTS_FAILED, 2=INTERRUPTED, 3=INTERNAL_ERROR, 4=USAGE_ERROR, 5=NO_TESTS."""


# ============================================================================
# Exceptions
# ============================================================================


class PytestError(Exception):
    """Base exception for pytest operations."""

    pass


class FixtureError(PytestError):
    """Raised when a fixture fails."""

    pass


class UsageError(PytestError):
    """Raised for incorrect pytest usage."""

    pass


class CollectionError(PytestError):
    """Raised when test collection fails."""

    pass


class Failed(PytestError):
    """Raised when a test fails via pytest.fail()."""

    def __init__(self, msg: str = "", pytrace: bool = True) -> None:
        super().__init__(msg)
        self.msg = msg
        self.pytrace = pytrace


class Skipped(PytestError):
    """Raised when a test is skipped via pytest.skip()."""

    def __init__(
        self,
        msg: str = "",
        allow_module_level: bool = False,
    ) -> None:
        super().__init__(msg)
        self.msg = msg
        self.allow_module_level = allow_module_level


class XFail(PytestError):
    """Raised when a test is marked as expected failure."""

    def __init__(self, reason: str = "") -> None:
        super().__init__(reason)
        self.reason = reason


# ============================================================================
# Data Classes
# ============================================================================


@dataclass(frozen=True, slots=True)
class MarkInfo:
    """Information about a pytest mark.

    Attributes:
        name: Mark name (e.g., "skip", "parametrize")
        args: Positional arguments to the mark
        kwargs: Keyword arguments to the mark
    """

    name: str
    args: tuple[Any, ...] = field(default_factory=tuple)
    kwargs: dict[str, Any] = field(default_factory=lambda: {})


@dataclass(frozen=True, slots=True)
class FixtureInfo:
    """Information about a pytest fixture.

    Attributes:
        name: Fixture name (function name by default)
        scope: Fixture scope (function, class, module, session)
        autouse: Whether fixture is automatically used
        params: Parametrization values if any
        ids: Custom IDs for parametrized values
    """

    name: str
    scope: FixtureScope = "function"
    autouse: bool = False
    params: tuple[Any, ...] | None = None
    ids: tuple[str, ...] | None = None


@dataclass(frozen=True, slots=True)
class TestItem:
    """Represents a collected test item.

    Attributes:
        nodeid: Unique test node ID (e.g., "test_file.py::test_func")
        name: Test name
        path: Path to test file
        lineno: Line number in source file
        marks: Applied marks
        keywords: Test keywords for filtering
    """

    nodeid: str
    name: str
    path: str
    lineno: int
    marks: tuple[MarkInfo, ...] = field(default_factory=tuple)
    keywords: frozenset[str] = field(default_factory=lambda: frozenset())


@dataclass(frozen=True, slots=True)
class TestReport:
    """Test execution report.

    Attributes:
        nodeid: Test node ID
        outcome: Test outcome (passed, failed, etc.)
        duration: Test duration in seconds
        longrepr: Long representation of failure
        sections: Captured output sections
        when: When the report was generated (setup, call, teardown)
    """

    nodeid: str
    outcome: TestOutcome
    duration: float
    longrepr: str | None = None
    sections: tuple[tuple[str, str], ...] = field(default_factory=tuple)
    when: Literal["setup", "call", "teardown"] = "call"


@dataclass(slots=True)
class CollectionResult:
    """Test collection results.

    Attributes:
        items: Collected test items
        errors: Collection errors
        warnings: Collection warnings
    """

    items: list[TestItem] = field(default_factory=lambda: [])
    errors: list[str] = field(default_factory=lambda: [])
    warnings: list[str] = field(default_factory=lambda: [])


@dataclass(slots=True)
class SessionResult:
    """Test session results.

    Attributes:
        exit_code: Session exit code
        passed: Number of passed tests
        failed: Number of failed tests
        skipped: Number of skipped tests
        errors: Number of errors
        xfailed: Number of expected failures
        xpassed: Number of unexpected passes
        duration: Total session duration
        reports: Individual test reports
    """

    exit_code: ExitCode = 0
    passed: int = 0
    failed: int = 0
    skipped: int = 0
    errors: int = 0
    xfailed: int = 0
    xpassed: int = 0
    duration: float = 0.0
    reports: list[TestReport] = field(default_factory=lambda: [])


# ============================================================================
# Protocols
# ============================================================================


@runtime_checkable
class FixtureRequestProtocol(Protocol):
    """Protocol for pytest fixture request object.

    Provides access to the requesting test context.
    """

    @property
    def fixturename(self) -> str:
        """Name of the fixture being requested."""
        ...

    @property
    def scope(self) -> FixtureScope:
        """Scope of the fixture."""
        ...

    @property
    def node(self) -> "ItemProtocol":
        """The test item requesting the fixture."""
        ...

    @property
    def param(self) -> Any:
        """Current parameter value for parametrized fixtures."""
        ...

    @property
    def param_index(self) -> int:
        """Index of the current parameter."""
        ...

    def addfinalizer(self, finalizer: Callable[[], None]) -> None:
        """Add a finalizer to be called after fixture teardown.

        Args:
            finalizer: Cleanup function to call
        """
        ...

    def applymarker(self, marker: "MarkDecoratorProtocol") -> None:
        """Apply a marker to the test.

        Args:
            marker: Marker to apply
        """
        ...

    def getfixturevalue(self, argname: str) -> Any:
        """Get the value of another fixture.

        Args:
            argname: Name of the fixture

        Returns:
            Fixture value
        """
        ...


@runtime_checkable
class ItemProtocol(Protocol):
    """Protocol for pytest test item."""

    @property
    def nodeid(self) -> str:
        """Unique node identifier."""
        ...

    @property
    def name(self) -> str:
        """Test name."""
        ...

    @property
    def path(self) -> Any:
        """Path to test file."""
        ...

    @property
    def keywords(self) -> dict[str, Any]:
        """Test keywords."""
        ...

    def add_marker(
        self,
        marker: "str | MarkDecoratorProtocol",
        append: bool = True,
    ) -> None:
        """Add a marker to the item.

        Args:
            marker: Marker name or decorator
            append: Whether to append or prepend
        """
        ...

    def iter_markers(
        self,
        name: str | None = None,
    ) -> Iterator[MarkInfo]:
        """Iterate over markers.

        Args:
            name: Filter by marker name

        Yields:
            MarkInfo for each marker
        """
        ...


@runtime_checkable
class MarkDecoratorProtocol(Protocol):
    """Protocol for pytest mark decorators."""

    @property
    def name(self) -> str:
        """Mark name."""
        ...

    @property
    def args(self) -> tuple[Any, ...]:
        """Mark arguments."""
        ...

    @property
    def kwargs(self) -> dict[str, Any]:
        """Mark keyword arguments."""
        ...

    def __call__(self, func: Callable[P, R]) -> Callable[P, R]:
        """Apply mark to a function.

        Args:
            func: Function to mark

        Returns:
            Marked function
        """
        ...

    def with_args(self, *args: Any, **kwargs: Any) -> "MarkDecoratorProtocol":
        """Create a new mark with additional arguments.

        Returns:
            New mark decorator
        """
        ...


@runtime_checkable
class ConfigProtocol(Protocol):
    """Protocol for pytest configuration."""

    @property
    def rootdir(self) -> Any:
        """Root directory for the test session."""
        ...

    @property
    def inipath(self) -> Any | None:
        """Path to pytest.ini or pyproject.toml."""
        ...

    def getini(self, name: str) -> Any:
        """Get an ini-file option.

        Args:
            name: Option name

        Returns:
            Option value
        """
        ...

    def getoption(self, name: str, default: Any = None) -> Any:
        """Get a command-line option.

        Args:
            name: Option name
            default: Default value

        Returns:
            Option value
        """
        ...

    def addinivalue_line(self, name: str, line: str) -> None:
        """Add a line to an ini-file option.

        Args:
            name: Option name
            line: Line to add
        """
        ...


@runtime_checkable
class SessionProtocol(Protocol):
    """Protocol for pytest session."""

    @property
    def config(self) -> ConfigProtocol:
        """Session configuration."""
        ...

    @property
    def items(self) -> list[ItemProtocol]:
        """Collected test items."""
        ...

    @property
    def testsfailed(self) -> int:
        """Number of failed tests."""
        ...

    @property
    def testscollected(self) -> int:
        """Number of collected tests."""
        ...


@runtime_checkable
class CaptureFixtureProtocol(Protocol[T_co]):
    """Protocol for pytest capture fixtures (capsys, capfd, etc.)."""

    def readouterr(self) -> tuple[str, str]:
        """Read captured output and error.

        Returns:
            Tuple of (stdout, stderr)
        """
        ...

    @contextmanager
    def disabled(self) -> Generator[None, None, None]:
        """Temporarily disable capture.

        Yields:
            None
        """
        ...


@runtime_checkable
class MonkeyPatchProtocol(Protocol):
    """Protocol for pytest monkeypatch fixture."""

    def setattr(
        self,
        target: object | str,
        name: str | object,
        value: object = ...,
        raising: bool = True,
    ) -> None:
        """Set an attribute.

        Args:
            target: Target object or import path
            name: Attribute name or value if target is string
            value: Value to set
            raising: Raise if attribute doesn't exist
        """
        ...

    def delattr(
        self,
        target: object | str,
        name: str = ...,
        raising: bool = True,
    ) -> None:
        """Delete an attribute.

        Args:
            target: Target object or import path
            name: Attribute name
            raising: Raise if attribute doesn't exist
        """
        ...

    def setitem(self, dic: dict[Any, Any], name: Any, value: Any) -> None:
        """Set a dictionary item.

        Args:
            dic: Target dictionary
            name: Key
            value: Value
        """
        ...

    def delitem(self, dic: dict[Any, Any], name: Any, raising: bool = True) -> None:
        """Delete a dictionary item.

        Args:
            dic: Target dictionary
            name: Key
            raising: Raise if key doesn't exist
        """
        ...

    def setenv(self, name: str, value: str, prepend: str | None = None) -> None:
        """Set an environment variable.

        Args:
            name: Variable name
            value: Variable value
            prepend: Prepend to existing value with separator
        """
        ...

    def delenv(self, name: str, raising: bool = True) -> None:
        """Delete an environment variable.

        Args:
            name: Variable name
            raising: Raise if variable doesn't exist
        """
        ...

    def syspath_prepend(self, path: str) -> None:
        """Prepend a path to sys.path.

        Args:
            path: Path to prepend
        """
        ...

    def chdir(self, path: str) -> None:
        """Change current directory.

        Args:
            path: New directory
        """
        ...

    def undo(self) -> None:
        """Undo all patches."""
        ...


@runtime_checkable
class TmpPathFactoryProtocol(Protocol):
    """Protocol for pytest tmp_path_factory fixture."""

    def mktemp(
        self,
        basename: str,
        numbered: bool = True,
    ) -> Any:
        """Create a temporary directory.

        Args:
            basename: Base name for directory
            numbered: Add a unique number suffix

        Returns:
            Path to the temporary directory
        """
        ...

    def getbasetemp(self) -> Any:
        """Get the base temporary directory.

        Returns:
            Path to base temp directory
        """
        ...


@runtime_checkable
class RecorderProtocol(Protocol):
    """Protocol for pytest warning/log recorders."""

    def pop(self, cls: type[Warning] = Warning) -> Any:
        """Pop a recorded warning.

        Args:
            cls: Warning class to filter

        Returns:
            Warning record
        """
        ...

    def clear(self) -> None:
        """Clear all recorded warnings."""
        ...

    def __len__(self) -> int:
        """Number of recorded warnings."""
        ...

    def __iter__(self) -> Iterator[Any]:
        """Iterate over recorded warnings."""
        ...


@runtime_checkable
class RaisesContextProtocol(Protocol[T_exc]):
    """Protocol for pytest.raises context manager."""

    @property
    def value(self) -> T_exc:
        """The caught exception instance."""
        ...

    @property
    def type(self) -> type[T_exc]:
        """The exception type."""
        ...

    @property
    def tb(self) -> TracebackType | None:
        """The exception traceback."""
        ...

    def match(self, pattern: str) -> bool:
        """Match exception message against pattern.

        Args:
            pattern: Regex pattern

        Returns:
            True if pattern matches
        """
        ...


# ============================================================================
# Fixture Decorator Protocol
# ============================================================================


@runtime_checkable
class FixtureDecoratorProtocol(Protocol):
    """Protocol for @pytest.fixture decorator."""

    @overload
    def __call__(self, func: Callable[P, R], /) -> Callable[P, R]: ...

    @overload
    def __call__(
        self,
        func: None = None,
        /,
        *,
        scope: FixtureScope = "function",
        params: Sequence[Any] | None = None,
        autouse: bool = False,
        ids: Sequence[str] | Callable[[Any], str | None] | None = None,
        name: str | None = None,
    ) -> Callable[[Callable[P, R]], Callable[P, R]]: ...

    def __call__(
        self,
        func: Callable[P, R] | None = None,
        /,
        *,
        scope: FixtureScope = "function",
        params: Sequence[Any] | None = None,
        autouse: bool = False,
        ids: Sequence[str] | Callable[[Any], str | None] | None = None,
        name: str | None = None,
    ) -> Callable[P, R] | Callable[[Callable[P, R]], Callable[P, R]]:
        """Create a fixture.

        Args:
            func: Fixture function (if used without arguments)
            scope: Fixture scope
            params: Parametrization values
            autouse: Auto-use fixture
            ids: Custom IDs for parameters
            name: Custom fixture name

        Returns:
            Fixture decorator or decorated function
        """
        ...


# ============================================================================
# Main Pytest Protocol
# ============================================================================


@runtime_checkable
class PytestProtocol(Protocol):
    """Unified protocol for pytest module."""

    # Marks
    @property
    def mark(self) -> Any:
        """Access pytest marks (skip, parametrize, etc.)."""
        ...

    # Exceptions
    @staticmethod
    def fail(msg: str = "", pytrace: bool = True) -> None:
        """Fail the test with a message.

        Args:
            msg: Failure message
            pytrace: Show Python traceback
        """
        ...

    @staticmethod
    def skip(msg: str = "", allow_module_level: bool = False) -> None:
        """Skip the test.

        Args:
            msg: Skip reason
            allow_module_level: Allow skipping at module level
        """
        ...

    @staticmethod
    def xfail(reason: str = "") -> None:
        """Mark test as expected failure.

        Args:
            reason: Reason for expected failure
        """
        ...

    @staticmethod
    def exit(msg: str = "", returncode: int = 0) -> None:
        """Exit the test session.

        Args:
            msg: Exit message
            returncode: Exit code
        """
        ...

    # Context managers
    @overload
    @staticmethod
    def raises(
        expected_exception: type[T_exc],
        *,
        match: str | None = None,
    ) -> RaisesContextProtocol[T_exc]: ...

    @overload
    @staticmethod
    def raises(
        expected_exception: tuple[type[T_exc], ...],
        *,
        match: str | None = None,
    ) -> RaisesContextProtocol[T_exc]: ...

    @staticmethod
    def raises(
        expected_exception: type[T_exc] | tuple[type[T_exc], ...],
        *,
        match: str | None = None,
    ) -> RaisesContextProtocol[T_exc]:
        """Assert that an exception is raised.

        Args:
            expected_exception: Exception type(s)
            match: Regex pattern to match

        Returns:
            Context manager for exception
        """
        ...

    @staticmethod
    def warns(
        expected_warning: type[Warning] | tuple[type[Warning], ...],
        *,
        match: str | None = None,
    ) -> RecorderProtocol:
        """Assert that a warning is raised.

        Args:
            expected_warning: Warning type(s)
            match: Regex pattern to match

        Returns:
            Warning recorder
        """
        ...

    @staticmethod
    def deprecated_call(
        *,
        match: str | None = None,
    ) -> RecorderProtocol:
        """Assert a deprecation warning is raised.

        Args:
            match: Regex pattern to match

        Returns:
            Warning recorder
        """
        ...

    # Decorators
    @property
    def fixture(self) -> FixtureDecoratorProtocol:
        """Fixture decorator."""
        ...

    # Introspection
    @staticmethod
    def approx(
        expected: Any,
        rel: float | None = None,
        abs: float | None = None,
        nan_ok: bool = False,
    ) -> Any:
        """Assert approximate equality.

        Args:
            expected: Expected value
            rel: Relative tolerance
            abs: Absolute tolerance
            nan_ok: Allow NaN comparisons

        Returns:
            Approx wrapper for comparison
        """
        ...

    # Main entry point
    @staticmethod
    def main(
        args: Sequence[str] | None = None,
        plugins: Sequence[Any] | None = None,
    ) -> ExitCode:
        """Run pytest with given arguments.

        Args:
            args: Command-line arguments
            plugins: Plugins to load

        Returns:
            Exit code
        """
        ...


# ============================================================================
# Assertion Helpers
# ============================================================================


def assert_equal(actual: T, expected: T, msg: str = "") -> None:
    """Assert two values are equal.

    Args:
        actual: Actual value
        expected: Expected value
        msg: Optional message on failure

    Raises:
        AssertionError: If values are not equal
    """
    if actual != expected:
        failure_msg = f"Expected {expected!r}, got {actual!r}"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_not_equal(actual: T, expected: T, msg: str = "") -> None:
    """Assert two values are not equal.

    Args:
        actual: Actual value
        expected: Expected value
        msg: Optional message on failure

    Raises:
        AssertionError: If values are equal
    """
    if actual == expected:
        failure_msg = f"Expected values to differ, both are {actual!r}"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_true(value: Any, msg: str = "") -> None:
    """Assert value is truthy.

    Args:
        value: Value to check
        msg: Optional message on failure

    Raises:
        AssertionError: If value is not truthy
    """
    if not value:
        failure_msg = f"Expected truthy value, got {value!r}"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_false(value: Any, msg: str = "") -> None:
    """Assert value is falsy.

    Args:
        value: Value to check
        msg: Optional message on failure

    Raises:
        AssertionError: If value is not falsy
    """
    if value:
        failure_msg = f"Expected falsy value, got {value!r}"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_is(actual: Any, expected: Any, msg: str = "") -> None:
    """Assert two objects are identical.

    Args:
        actual: Actual object
        expected: Expected object
        msg: Optional message on failure

    Raises:
        AssertionError: If objects are not identical
    """
    if actual is not expected:
        failure_msg = f"Expected {expected!r} is {actual!r}"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_is_not(actual: Any, expected: Any, msg: str = "") -> None:
    """Assert two objects are not identical.

    Args:
        actual: Actual object
        expected: Expected object
        msg: Optional message on failure

    Raises:
        AssertionError: If objects are identical
    """
    if actual is expected:
        failure_msg = "Expected objects to differ by identity"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_is_none(value: Any, msg: str = "") -> None:
    """Assert value is None.

    Args:
        value: Value to check
        msg: Optional message on failure

    Raises:
        AssertionError: If value is not None
    """
    if value is not None:
        failure_msg = f"Expected None, got {value!r}"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_is_not_none(value: Any, msg: str = "") -> None:
    """Assert value is not None.

    Args:
        value: Value to check
        msg: Optional message on failure

    Raises:
        AssertionError: If value is None
    """
    if value is None:
        failure_msg = "Expected non-None value"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_in(member: Any, container: Any, msg: str = "") -> None:
    """Assert member is in container.

    Args:
        member: Member to find
        container: Container to search
        msg: Optional message on failure

    Raises:
        AssertionError: If member not in container
    """
    if member not in container:
        failure_msg = f"Expected {member!r} in {container!r}"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_not_in(member: Any, container: Any, msg: str = "") -> None:
    """Assert member is not in container.

    Args:
        member: Member to check
        container: Container to search
        msg: Optional message on failure

    Raises:
        AssertionError: If member is in container
    """
    if member in container:
        failure_msg = f"Expected {member!r} not in {container!r}"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_isinstance(obj: Any, cls: type | tuple[type, ...], msg: str = "") -> None:
    """Assert object is instance of class.

    Args:
        obj: Object to check
        cls: Class or tuple of classes
        msg: Optional message on failure

    Raises:
        AssertionError: If not an instance
    """
    if not isinstance(obj, cls):
        failure_msg = f"Expected instance of {cls}, got {type(obj)}"
        if msg:
            failure_msg = f"{msg}: {failure_msg}"
        raise AssertionError(failure_msg)


def assert_raises(
    exception: type[BaseException] | tuple[type[BaseException], ...],
    func: Callable[..., Any],
    *args: Any,
    **kwargs: Any,
) -> BaseException:
    """Assert function raises exception.

    Args:
        exception: Exception type(s) expected
        func: Function to call
        *args: Function arguments
        **kwargs: Function keyword arguments

    Returns:
        The raised exception

    Raises:
        AssertionError: If exception not raised
    """
    try:
        func(*args, **kwargs)
    except exception as e:
        return e
    except BaseException as e:
        raise AssertionError(
            f"Expected {exception}, got {type(e).__name__}: {e}"
        ) from e
    raise AssertionError(f"Expected {exception} to be raised")


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Type Aliases
    "FixtureScope",
    "MarkName",
    "ExitCode",
    # Enums
    "ExitCodeEnum",
    "TestOutcome",
    # Exceptions
    "PytestError",
    "FixtureError",
    "UsageError",
    "CollectionError",
    "Failed",
    "Skipped",
    "XFail",
    # Data Classes
    "MarkInfo",
    "FixtureInfo",
    "TestItem",
    "TestReport",
    "CollectionResult",
    "SessionResult",
    # Protocols
    "FixtureRequestProtocol",
    "ItemProtocol",
    "MarkDecoratorProtocol",
    "ConfigProtocol",
    "SessionProtocol",
    "CaptureFixtureProtocol",
    "MonkeyPatchProtocol",
    "TmpPathFactoryProtocol",
    "RecorderProtocol",
    "RaisesContextProtocol",
    "FixtureDecoratorProtocol",
    "PytestProtocol",
    # Assertion Helpers
    "assert_equal",
    "assert_not_equal",
    "assert_true",
    "assert_false",
    "assert_is",
    "assert_is_not",
    "assert_is_none",
    "assert_is_not_none",
    "assert_in",
    "assert_not_in",
    "assert_isinstance",
    "assert_raises",
]
