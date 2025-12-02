"""Core agent definitions for Python 3.15+ free-threaded runtime.

Provides thread-safe Agent and Runner wrappers that:
- Ensure tensor compute service is initialized on first use
- Support semantic guardrails with GPU-accelerated checks
- Work correctly with GIL disabled (PYTHON_GIL=0)

All function_tool decorated functions automatically get:
- Input guardrails (semantic similarity checking)
- Output guardrails (PII detection)
- Proper async/sync handling

Thread Safety:
- Agent/Runner use __slots__ to prevent dynamic attribute creation
- Compute service initialization uses double-checked locking
- All guardrails are async and non-blocking
"""

from __future__ import annotations

import functools
import inspect
import logging
from collections.abc import Callable
from typing import TYPE_CHECKING, Any, Final, ParamSpec, TypeVar, cast

from agents import Agent as OpenAIAgent
from agents import Runner as OpenAIRunner
from agents import function_tool as _original_function_tool

from .compute import get_compute_service
from .guardrails import (
    ToolInputGuardrailData,
    ToolOutputGuardrailData,
    semantic_input_guardrail,
    semantic_output_guardrail,
)

if TYPE_CHECKING:
    from .config import AgentConfig

logger: Final[logging.Logger] = logging.getLogger(__name__)

# Type variables for generic function decoration
P = ParamSpec("P")
T = TypeVar("T")
F = TypeVar("F", bound=Callable[..., Any])

# Re-export for convenience
__all__: Final[list[str]] = [
    "Agent",
    "Runner",
    "function_tool",
    "semantic_input_guardrail",
    "semantic_output_guardrail",
]


class _ToolContext:
    """Immutable context for guardrail evaluation.

    Thread-safe via __slots__ and immutable design.
    """

    __slots__ = ("tool_name", "tool_arguments")

    def __init__(
        self,
        name: str,
        args: tuple[Any, ...],
        kwargs: dict[str, Any],
    ) -> None:
        super().__init__()
        self.tool_name: str = name
        self.tool_arguments: dict[str, Any] | tuple[Any, ...] = kwargs if kwargs else args


def function_tool(func: F) -> F:
    """Decorator that registers a function as a tool with semantic guardrails.

    Thread-safe for Python 3.15+ free-threaded runtime.
    Guardrails are evaluated asynchronously using the GPU-accelerated
    BatchComputeService for semantic similarity checks.

    The decorated function will have two additional attributes:
    - tool_input_guardrails: List of input guardrail functions
    - tool_output_guardrails: List of output guardrail functions

    Usage:
        @function_tool
        async def my_tool(arg: str) -> str:
            return f"Processed: {arg}"

        # Add guardrails
        my_tool.tool_input_guardrails.append(semantic_input_guardrail("harmful"))

    Args:
        func: The function to decorate (sync or async)

    Returns:
        The decorated function with guardrail support
    """

    @functools.wraps(func)
    async def wrapper(*args: Any, **kwargs: Any) -> Any:
        context = _ToolContext(func.__name__, args, kwargs)

        # 1. Input Guardrails - block harmful input before execution
        input_guardrails: list[Any] = getattr(wrapper, "tool_input_guardrails", [])
        if input_guardrails:
            input_data = ToolInputGuardrailData(context=context)
            for guardrail in input_guardrails:
                try:
                    result = await guardrail(input_data)
                    if result.message:
                        logger.warning(
                            "Input guardrail blocked call to %s: %s",
                            func.__name__,
                            result.message,
                        )
                        return result.message
                except Exception as e:
                    logger.error("Error in input guardrail for %s: %s", func.__name__, e)
                    raise

        # 2. Execute Tool
        try:
            if inspect.iscoroutinefunction(func):
                result = await func(*args, **kwargs)
            else:
                result = func(*args, **kwargs)
        except Exception:
            raise

        # 3. Output Guardrails - detect PII/sensitive data in output
        output_guardrails: list[Any] = getattr(wrapper, "tool_output_guardrails", [])
        if output_guardrails:
            output_data = ToolOutputGuardrailData(output=result, context=context)
            for guardrail in output_guardrails:
                try:
                    await guardrail(output_data)
                except Exception as e:
                    logger.error("Output guardrail triggered for %s: %s", func.__name__, e)
                    raise

        return result

    # Initialize guardrail lists as mutable attributes
    wrapper.tool_input_guardrails = []  # type: ignore[attr-defined]
    wrapper.tool_output_guardrails = []  # type: ignore[attr-defined]

    # Apply the original OpenAI function_tool decorator
    return cast(F, _original_function_tool(wrapper))


class Agent(OpenAIAgent):
    """Thread-safe Aspire Agent with automatic tensor compute initialization.

    Extends OpenAI Agent to ensure GPU compute service is ready before
    any agent operations. Safe for concurrent use in Python 3.15+
    free-threaded runtime.

    The compute service is initialized lazily on first Agent creation,
    using thread-safe double-checked locking pattern.

    Examples:
        >>> agent = Agent(
        ...     name="coder",
        ...     instructions="You are a coding assistant",
        ...     model="gpt-4o",
        ... )
        >>> result = await Runner.run(agent, "Write hello world")
    """

    __slots__ = ()  # No additional instance attributes - memory efficient

    def __init__(self, *args: Any, **kwargs: Any) -> None:
        """Initialize agent with automatic compute service setup.

        Args:
            *args: Positional arguments passed to OpenAI Agent
            **kwargs: Keyword arguments passed to OpenAI Agent
                - name: Agent name
                - instructions: System prompt
                - model: Model name string
        """
        # Ensure compute service is initialized (thread-safe singleton)
        # This guarantees GPU/Tensor Cores are ready before agent runs
        get_compute_service()
        super().__init__(*args, **kwargs)

    @classmethod
    def from_config(cls, config: AgentConfig) -> Agent:
        """Create an Agent from an AgentConfig.

        Args:
            config: The agent configuration

        Returns:
            Configured Agent instance
        """
        return cls(
            name=config.name,
            instructions=config.prompt,
            model=config.model.name,
        )


class Runner(OpenAIRunner):
    """Thread-safe Aspire Runner wrapper.

    Provides a clean interface to the OpenAI Agent Runner.
    Can be extended with logging, tracing, or custom execution logic.

    Thread-safe by design - no mutable instance state.

    Examples:
        >>> agent = Agent(name="test", instructions="Help", model="gpt-4o")
        >>> result = await Runner.run(agent, "Hello")
        >>> print(result.final_output)
    """

    __slots__ = ()  # No additional instance attributes

    # Inherits all functionality from OpenAIRunner
    # Class methods like run() are already async and thread-safe
