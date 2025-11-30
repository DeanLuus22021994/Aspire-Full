"""Core definitions for Aspire Agents."""

import functools
import inspect
import logging
from typing import Any, Callable, cast

from agents import Agent as OpenAIAgent  # type: ignore # pylint: disable=import-error
from agents import Runner as OpenAIRunner  # type: ignore # pylint: disable=import-error
from agents import function_tool as _original_function_tool  # type: ignore # pylint: disable=import-error

from .compute import get_compute_service
from .guardrails import (
    ToolInputGuardrailData,
    ToolOutputGuardrailData,
    semantic_input_guardrail,
    semantic_output_guardrail,
)

logger = logging.getLogger(__name__)

# Re-export function_tool
__all__ = [
    "Agent",
    "Runner",
    "function_tool",
    "semantic_input_guardrail",
    "semantic_output_guardrail",
]


def function_tool(func: Callable) -> Callable:
    """
    Decorator that registers a function as a tool and enables semantic guardrails.
    """

    @functools.wraps(func)
    async def wrapper(*args, **kwargs):
        # Context for guardrails
        class ToolContext:
            def __init__(self, name, args, kwargs):
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
    """
    Aspire Agent wrapper that ensures tensor compute is ready.
    """

    def __init__(self, *args: Any, **kwargs: Any):
        # Ensure compute service is initialized (and GPU is ready) when Agent is created
        get_compute_service()
        super().__init__(*args, **kwargs)


class Runner(OpenAIRunner):
    """
    Aspire Runner wrapper.
    """

    # We can add custom logic here if needed, e.g. logging or tracing
