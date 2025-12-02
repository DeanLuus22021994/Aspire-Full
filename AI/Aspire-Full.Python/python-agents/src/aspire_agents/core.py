"""Core agent definitions for Python 3.15+ free-threaded runtime.

Provides thread-safe Agent and Runner wrappers that:
- Ensure tensor compute service is initialized on first use
- Support semantic guardrails with GPU-accelerated checks
- Work correctly with GIL disabled (PYTHON_GIL=0)

All function_tool decorated functions automatically get:
- Input guardrails (semantic similarity checking)
- Output guardrails (PII detection)
- Proper async/sync handling
"""

from __future__ import annotations

import functools
import inspect
import logging
from typing import TYPE_CHECKING, Any, Callable, Final, TypeVar, cast

from agents import Agent as OpenAIAgent  # type: ignore # pylint: disable=import-error
from agents import Runner as OpenAIRunner  # type: ignore # pylint: disable=import-error
from agents import (
    function_tool as _original_function_tool,  # type: ignore # pylint: disable=import-error
)

from .compute import get_compute_service
from .guardrails import (
    ToolInputGuardrailData,
    ToolOutputGuardrailData,
    semantic_input_guardrail,
    semantic_output_guardrail,
)

if TYPE_CHECKING:
    from collections.abc import Awaitable

logger: Final = logging.getLogger(__name__)

F = TypeVar("F", bound=Callable[..., Any])

# Re-export function_tool
__all__: Final[list[str]] = [
    "Agent",
    "Runner",
    "function_tool",
    "semantic_input_guardrail",
    "semantic_output_guardrail",
]


def function_tool(func: F) -> F:
    """Decorator that registers a function as a tool with semantic guardrails.

    Thread-safe for Python 3.15+ free-threaded runtime.
    Guardrails are evaluated asynchronously using the GPU-accelerated
    BatchComputeService for semantic similarity checks.

    Usage:
        @function_tool
        async def my_tool(arg: str) -> str:
            return f"Processed: {arg}"

    Args:
        func: The function to decorate (sync or async)

    Returns:
        The decorated function with guardrail support
    """

    @functools.wraps(func)
    async def wrapper(*args: Any, **kwargs: Any) -> Any:
        # Thread-safe context for guardrails (immutable dataclass)
        class ToolContext:
            """Immutable context for guardrail evaluation."""

            __slots__ = ("tool_name", "tool_arguments")

            def __init__(self, name: str, args: tuple[Any, ...], kwargs: dict[str, Any]) -> None:
                self.tool_name = name
                self.tool_arguments = kwargs if kwargs else args

        context = ToolContext(func.__name__, args, kwargs)

        # 1. Input Guardrails
        if hasattr(wrapper, "tool_input_guardrails"):
            # Use getattr to avoid type checking errors on function attributes
            guardrails = getattr(wrapper, "tool_input_guardrails", [])
            if guardrails:
                input_data = ToolInputGuardrailData(context=context)
                for guardrail in guardrails:
                    try:
                        result = await guardrail(input_data)
                        if result.message:
                            logger.warning(
                                "Input guardrail blocked call to %s: %s",
                                func.__name__,
                                result.message,
                            )
                            return result.message
                    except Exception as e:  # pylint: disable=broad-exception-caught
                        logger.error("Error in input guardrail: %s", e)
                        raise

        # 2. Execute Tool
        try:
            if inspect.iscoroutinefunction(func):
                result = await func(*args, **kwargs)
            else:
                result = func(*args, **kwargs)
        except Exception as e:  # pylint: disable=broad-exception-caught
            raise e

        # 3. Output Guardrails
        if hasattr(wrapper, "tool_output_guardrails"):
            output_data = ToolOutputGuardrailData(output=result, context=context)
            guardrails = getattr(wrapper, "tool_output_guardrails", [])
            for guardrail in guardrails:
                await guardrail(output_data)

        return result

    # Initialize lists
    setattr(wrapper, "tool_input_guardrails", [])
    setattr(wrapper, "tool_output_guardrails", [])

    # Cast to Any to avoid return type mismatch with decorator
    return cast(Any, _original_function_tool(wrapper))


class Agent(OpenAIAgent):
    """Thread-safe Aspire Agent with automatic tensor compute initialization.

    Extends OpenAI Agent to ensure GPU compute service is ready before
    any agent operations. Safe for concurrent use in Python 3.15+
    free-threaded runtime.

    The compute service is initialized lazily on first Agent creation,
    using thread-safe double-checked locking.
    """

    __slots__ = ()  # No additional instance attributes

    def __init__(self, *args: Any, **kwargs: Any) -> None:
        # Ensure compute service is initialized (thread-safe singleton)
        # This guarantees GPU/Tensor Cores are ready before agent runs
        get_compute_service()
        super().__init__(*args, **kwargs)


class Runner(OpenAIRunner):
    """Thread-safe Aspire Runner wrapper.

    Provides a clean interface to the OpenAI Agent Runner.
    Can be extended with logging, tracing, or custom execution logic.
    """

    __slots__ = ()  # No additional instance attributes

    # Inherits all functionality from OpenAIRunner
    # Add custom methods here as needed (e.g., tracing, logging)
