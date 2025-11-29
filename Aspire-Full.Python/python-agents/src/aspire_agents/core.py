"""Core definitions for Aspire Agents."""

import functools
import inspect
import logging
from typing import Any, Callable

from agents import Agent as OpenAIAgent
from agents import Runner as OpenAIRunner
from agents import function_tool as _original_function_tool

from .compute import get_compute_service
from .guardrails import (
    semantic_input_guardrail,
    semantic_output_guardrail,
    ToolInputGuardrailData,
    ToolOutputGuardrailData,
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
            input_data = ToolInputGuardrailData(context=context)
            for guardrail in wrapper.tool_input_guardrails:
                try:
                    result = await guardrail(input_data)
                    if result.message:
                        logger.warning(
                            f"Input guardrail blocked call to {func.__name__}: {result.message}"
                        )
                        return result.message
                except Exception as e:
                    logger.error(f"Error in input guardrail: {e}")
                    raise

        # 2. Execute Tool
        try:
            if inspect.iscoroutinefunction(func):
                result = await func(*args, **kwargs)
            else:
                result = func(*args, **kwargs)
        except Exception as e:
            raise e

        # 3. Output Guardrails
        if hasattr(wrapper, "tool_output_guardrails"):
            output_data = ToolOutputGuardrailData(output=result, context=context)
            for guardrail in wrapper.tool_output_guardrails:
                await guardrail(output_data)

        return result

    # Initialize lists
    wrapper.tool_input_guardrails = []
    wrapper.tool_output_guardrails = []

    return _original_function_tool(wrapper)


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
    pass
