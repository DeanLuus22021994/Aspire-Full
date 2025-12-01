"""High-level runner that wraps Aspire Agents core."""

from __future__ import annotations

import asyncio
from dataclasses import dataclass

from rich.console import Console

from .config import AgentConfig
from .core import Agent, Runner
from .gpu import TensorCoreInfo, ensure_tensor_core_gpu

console = Console()


@dataclass(slots=True)
class AgentResult:
    """Returned content plus downstream handoff identifiers."""

    content: str
    handoffs: list[str]


class AgentRunner:
    """Build and execute agents based on a manifest."""

    def __init__(self, config: AgentConfig):
        self.config = config
        # Ensure GPU is ready immediately
        self.tensor_info: TensorCoreInfo = ensure_tensor_core_gpu()

        # Initialize the agent using the unified core
        self.agent = Agent(
            name=config.name,
            instructions=config.prompt,
            model=config.model.name,
            # We could map temperature/top_p if Agent supports it,
            # or pass them via run options.
        )

    async def arun(self, user_input: str) -> AgentResult:
        """Execute the agent asynchronously."""
        # Use the core Runner to execute the agent
        result = await Runner.run(self.agent, user_input)

        return AgentResult(
            content=str(result.final_output),
            handoffs=self.config.handoffs,
        )

    def run(self, user_input: str) -> AgentResult:
        """Synchronous helper for consumers that cannot await."""
        return asyncio.run(self.arun(user_input))

    def pretty_print(self, result: AgentResult) -> None:
        """Render agent output and handoffs to the console."""
        console.rule(f"{self.config.name} :: output")
        console.print(result.content)
        if result.handoffs:
            console.rule("handoffs")
            for handoff in result.handoffs:
                console.print(f"- {handoff}")
