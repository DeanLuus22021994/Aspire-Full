"""OpenAI Agents SDK abstractions.

Provides protocol definitions for the openai-agents package
without requiring installation.
"""

from __future__ import annotations

from collections.abc import Awaitable, Callable, Sequence
from dataclasses import dataclass
from typing import (
    TYPE_CHECKING,
    Any,
    Final,
    Generic,
    Protocol,
    TypeVar,
    cast,
    runtime_checkable,
)

# ============================================================================
# Type Variables
# ============================================================================

T = TypeVar("T")
# Invariant for dataclasses
OutputT = TypeVar("OutputT")
# Covariant for protocols
OutputT_co = TypeVar("OutputT_co", covariant=True)


# ============================================================================
# Agent Protocol
# ============================================================================


@runtime_checkable
class AgentProtocol(Protocol):
    """Protocol for OpenAI Agent interface.

    Defines the core agent capabilities.
    """

    @property
    def name(self) -> str:
        """Agent name."""
        ...

    @property
    def instructions(self) -> str:
        """System instructions."""
        ...

    @property
    def model(self) -> str:
        """Model identifier."""
        ...

    @property
    def tools(self) -> Sequence[Any]:
        """Available tools."""
        ...

    @property
    def output_type(self) -> type[Any] | None:
        """Expected output type."""
        ...


# ============================================================================
# Runner Protocol
# ============================================================================


@runtime_checkable
class RunnerProtocol(Protocol):
    """Protocol for Agent Runner.

    Handles agent execution and streaming.
    """

    @classmethod
    async def run(
        cls,
        agent: AgentProtocol,
        input: str,  # noqa: A002
        *,
        context: Any | None = None,
        previous_response_id: str | None = None,
        **kwargs: Any,
    ) -> "RunResult[Any]":
        """Run agent with input.

        Args:
            agent: Agent to run
            input: User input
            context: Optional context object
            previous_response_id: Previous response for conversation
            **kwargs: Additional arguments

        Returns:
            RunResult with output
        """
        ...

    @classmethod
    def run_streamed(
        cls,
        agent: AgentProtocol,
        input: str,  # noqa: A002
        *,
        context: Any | None = None,
        **kwargs: Any,
    ) -> "StreamedRunResult[Any]":
        """Run agent with streaming output.

        Args:
            agent: Agent to run
            input: User input
            context: Optional context object
            **kwargs: Additional arguments

        Returns:
            StreamedRunResult for async iteration
        """
        ...


# ============================================================================
# Run Result
# ============================================================================


@dataclass
class RunResult(Generic[OutputT]):
    """Result from agent run.

    Contains the final output and metadata.
    """

    final_output: OutputT
    """The final output from the agent."""

    last_response_id: str | None = None
    """Response ID for conversation continuity."""

    usage: dict[str, int] | None = None
    """Token usage statistics."""

    metadata: dict[str, Any] | None = None
    """Additional metadata."""


@runtime_checkable
class StreamedRunResult(Protocol[OutputT_co]):
    """Streaming result from agent run."""

    @property
    def last_response_id(self) -> str | None:
        """Response ID for conversation continuity."""
        ...

    def stream_events(self) -> "AsyncIterator[StreamEvent]":
        """Iterate over stream events."""
        ...

    async def final_output(self) -> OutputT_co:
        """Get final output after streaming completes."""
        ...


# ============================================================================
# Stream Events
# ============================================================================


@dataclass(frozen=True)
class StreamEvent:
    """Event from streaming response."""

    type: str
    """Event type identifier."""

    data: Any
    """Event data payload."""


# ============================================================================
# Function Tool Protocol
# ============================================================================


@runtime_checkable
class FunctionToolProtocol(Protocol):
    """Protocol for function tools."""

    @property
    def name(self) -> str:
        """Tool name."""
        ...

    @property
    def description(self) -> str:
        """Tool description."""
        ...

    async def __call__(self, *args: Any, **kwargs: Any) -> Any:
        """Execute tool."""
        ...


# ============================================================================
# Output Schema
# ============================================================================


@dataclass
class AgentOutputSchema:
    """Schema for agent output type.

    Wraps an output type with validation settings.
    """

    output_type: type[Any]
    """The expected output type."""

    strict_json_schema: bool = True
    """Whether to enforce strict JSON schema validation."""

    def is_plain_text(self) -> bool:
        """Check if output is plain text."""
        return self.output_type is str

    def name(self) -> str:
        """Get schema name."""
        return self.output_type.__name__


# ============================================================================
# Guardrail Types
# ============================================================================


@dataclass(frozen=True)
class GuardrailResult:
    """Result from guardrail check."""

    passed: bool
    """Whether the check passed."""

    message: str | None = None
    """Message if check failed."""


@dataclass
class ToolInputGuardrailData:
    """Data passed to input guardrails."""

    context: Any
    """Tool context with name and arguments."""


@dataclass
class ToolOutputGuardrailData:
    """Data passed to output guardrails."""

    output: Any
    """Tool output value."""

    context: Any
    """Tool context."""


# Type alias for guardrail functions
InputGuardrail = Callable[[ToolInputGuardrailData], Awaitable[GuardrailResult]]
OutputGuardrail = Callable[[ToolOutputGuardrailData], Awaitable[GuardrailResult]]


# ============================================================================
# Agent Factory
# ============================================================================


def create_agent(
    *,
    name: str,
    instructions: str,
    model: str = "gpt-4o",
    tools: Sequence[Any] | None = None,
    output_type: type[Any] | AgentOutputSchema | None = None,
) -> AgentProtocol | Any:
    """Create an agent instance.

    Args:
        name: Agent name
        instructions: System instructions
        model: Model identifier
        tools: Available tools
        output_type: Expected output type

    Returns:
        Agent instance implementing AgentProtocol.

    Raises:
        RuntimeError: If openai-agents package is not installed.
    """
    try:
        # Dynamic import - module may not be installed
        import importlib

        agents_module: Any = importlib.import_module("agents")
        agent_class: Any = getattr(agents_module, "Agent")

        # Cast output_type to Any to avoid strict type checking issues
        # between our AgentOutputSchema and the SDK's AgentOutputSchemaBase
        _output_type: Any = output_type

        agent: Any = agent_class(
            name=name,
            instructions=instructions,
            model=model,
            tools=list(tools) if tools else [],
            output_type=_output_type,
        )
        return cast(AgentProtocol, agent)
    except ImportError as e:
        msg = "openai-agents package required. Install with: pip install openai-agents"
        raise RuntimeError(msg) from e


def function_tool(func: Callable[..., Any]) -> FunctionToolProtocol:
    """Decorator to register a function as a tool.

    Args:
        func: Function to register

    Returns:
        Decorated function implementing FunctionToolProtocol.

    Raises:
        RuntimeError: If openai-agents package is not installed.
    """
    try:
        # Dynamic import - module may not be installed
        import importlib

        agents_module: Any = importlib.import_module("agents")
        function_tool_decorator: Any = getattr(agents_module, "function_tool")
        decorated: Any = function_tool_decorator(func)
        return cast(FunctionToolProtocol, decorated)
    except ImportError as e:
        msg = "openai-agents package required. Install with: pip install openai-agents"
        raise RuntimeError(msg) from e


# ============================================================================
# Async Iterator (for streaming)
# ============================================================================

if TYPE_CHECKING:
    from collections.abc import AsyncIterator


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Protocols
    "AgentProtocol",
    "RunnerProtocol",
    "FunctionToolProtocol",
    # Results
    "RunResult",
    "StreamedRunResult",
    "StreamEvent",
    # Schema
    "AgentOutputSchema",
    # Guardrails
    "GuardrailResult",
    "ToolInputGuardrailData",
    "ToolOutputGuardrailData",
    "InputGuardrail",
    "OutputGuardrail",
    # Factory
    "create_agent",
    "function_tool",
]
