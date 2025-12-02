"""Thread-safe agent runner for Python 3.15+ free-threaded runtime.

Provides AgentRunner class that:
- Initializes tensor compute service before agent execution
- Validates GPU/Tensor Core availability
- Supports both sync and async execution patterns
- Handles handoffs between agents

All operations are thread-safe and work correctly with GIL disabled.
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Final

from rich.console import Console

from .config import AgentConfig
from .core import Agent, Runner
from .gpu import TensorCoreInfo, ensure_tensor_core_gpu

if TYPE_CHECKING:
    from agents.result import RunResult  # type: ignore

console: Final = Console()


@dataclass(frozen=True, slots=True)
class AgentResult:
    """Immutable result from agent execution.

    Thread-safe due to immutability (frozen=True).
    """

    content: str
    handoffs: tuple[str, ...] = field(default_factory=tuple)  # Immutable tuple

    @classmethod
    def from_run_result(cls, result: RunResult, handoffs: list[str]) -> AgentResult:
        """Create from OpenAI Runner result."""
        return cls(
            content=str(result.final_output),
            handoffs=tuple(handoffs),
        )


class AgentRunner:
    """Thread-safe agent runner with tensor compute integration.

    Manages agent lifecycle with automatic GPU/Tensor Core initialization.
    Supports both sync and async execution patterns.

    Thread Safety:
    - tensor_info is immutable (frozen dataclass)
    - Agent and Runner are instantiated per-runner (no shared state)
    - GPU initialization uses thread-safe singleton pattern
    """

    __slots__ = ("config", "tensor_info", "agent")

    def __init__(self, config: AgentConfig) -> None:
        self.config = config
        # Ensure GPU is ready immediately (thread-safe)
        self.tensor_info: TensorCoreInfo = ensure_tensor_core_gpu()

        # Initialize the agent using the unified core
        self.agent = Agent(
            name=config.name,
            instructions=config.prompt,
            model=config.model.name,
            # Temperature/top_p can be passed via run options if supported
        )

    async def arun(self, user_input: str) -> AgentResult:
        """Execute the agent asynchronously.

        Thread-safe: no shared mutable state during execution.
        """
        result = await Runner.run(self.agent, user_input)
        return AgentResult.from_run_result(result, self.config.handoffs)

    def run(self, user_input: str) -> AgentResult:
        """Synchronous execution helper.

        Creates a new event loop for non-async contexts.
        Consider using arun() directly in async code.
        """
        return asyncio.run(self.arun(user_input))

    def pretty_print(self, result: AgentResult) -> None:
        """Render agent output and handoffs to the console."""
        console.rule(f"{self.config.name} :: output")
        console.print(result.content)
        if result.handoffs:
            console.rule("handoffs")
            for handoff in result.handoffs:
                console.print(f"- {handoff}")
