"""Core definitions for Aspire Agents."""

from typing import Any

from agents import Agent as OpenAIAgent
from agents import Runner as OpenAIRunner
from agents import function_tool

from .compute import get_compute_service
from .guardrails import (
    semantic_input_guardrail,
    semantic_output_guardrail,
)

# Re-export function_tool
__all__ = [
    "Agent",
    "Runner",
    "function_tool",
    "semantic_input_guardrail",
    "semantic_output_guardrail",
]


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
