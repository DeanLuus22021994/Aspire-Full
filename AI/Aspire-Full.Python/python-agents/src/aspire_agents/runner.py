"""Thread-safe agent runner with Tensor Core compute integration.

Python 3.15+ free-threaded runtime (PYTHON_GIL=0) compatible.
All agent executions leverage GPU tensor compute for embeddings and inference.
"""

from __future__ import annotations

import asyncio
import logging
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any, Final

from .compute import get_compute_service
from .config import AgentConfig
from .core import Agent, Runner

if TYPE_CHECKING:
    from agents import RunResult

    from .compute import BatchComputeService

logger: Final[logging.Logger] = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class AgentResult:
    """Immutable result from an agent run.

    Thread-safe: frozen dataclass with slots for Python 3.15 free-threaded.

    Attributes:
        output: The final output from the agent
        handoffs: Tuple of agent names that were handed off to (immutable)
        metadata: Additional metadata from the run
    """

    output: str
    handoffs: tuple[str, ...] = field(default_factory=tuple)
    metadata: dict[str, Any] = field(default_factory=dict)

    @classmethod
    def from_run_result(
        cls,
        result: RunResult,
        handoffs: tuple[str, ...] | list[str] | None = None,
    ) -> AgentResult:
        """Create AgentResult from OpenAI Agents SDK RunResult.

        Args:
            result: The RunResult from agents.Runner.run()
            handoffs: Optional tuple/list of agent names that were handed off to

        Returns:
            Immutable AgentResult instance
        """
        # Convert list to tuple for immutability, or use empty tuple
        handoff_tuple: tuple[str, ...] = tuple(handoffs) if handoffs is not None else ()

        return cls(
            output=str(result.final_output) if result.final_output else "",
            handoffs=handoff_tuple,
            metadata={
                "last_agent": result.last_agent.name if result.last_agent else None,
            },
        )


class AgentRunner:
    """Thread-safe agent runner with integrated Tensor Core compute.

    Provides a high-level interface for running agents with:
    - Automatic GPU tensor compute for embeddings
    - Thread-safe execution via Python 3.15 free-threading
    - Configurable agent behavior via AgentConfig

    Attributes:
        config: Agent configuration (frozen dataclass)
        compute_service: BatchComputeService singleton for GPU compute
    """

    __slots__ = ("config", "compute_service", "_agent", "_runner")

    def __init__(
        self,
        config: AgentConfig | None = None,
        compute_service: BatchComputeService | None = None,
    ) -> None:
        """Initialize the agent runner.

        Args:
            config: Optional agent configuration. Uses defaults if not provided.
            compute_service: Optional compute service. Uses singleton if not provided.
        """
        self.config: AgentConfig = config or AgentConfig(
            name="default-agent",
            description="Default agent configuration",
            prompt="You are a helpful assistant.",
            model="gpt-4o-mini",
        )
        self.compute_service: BatchComputeService = compute_service or get_compute_service()

        # Initialize agent and runner (lazy - created on first run)
        self._agent: Agent | None = None
        self._runner: Runner | None = None

        logger.info(
            "AgentRunner initialized with model=%s, compute_service=%s",
            self.config.model.name,
            type(self.compute_service).__name__,
        )

    def _ensure_agent(self) -> Agent:
        """Ensure agent is initialized (lazy initialization)."""
        if self._agent is None:
            self._agent = Agent(
                name=self.config.name,
                instructions=self.config.prompt,
                model=self.config.model,
            )
        return self._agent

    async def run(self, prompt: str) -> AgentResult:
        """Run the agent with the given prompt.

        Uses the OpenAI Agents SDK Runner.run() method with integrated
        tensor compute for any embedding operations.

        Args:
            prompt: The user prompt to process

        Returns:
            AgentResult with output and metadata
        """
        agent = self._ensure_agent()

        logger.debug("Running agent '%s' with prompt: %s...", agent.name, prompt[:50])

        result = await Runner.run(agent, prompt)

        # Extract handoffs as tuple for immutability
        handoffs: tuple[str, ...] = ()
        result_handoffs = getattr(result, "handoffs", None)
        if result_handoffs:
            handoffs = tuple(str(h) for h in result_handoffs)

        return AgentResult.from_run_result(result, handoffs)

    async def run_with_embedding(self, prompt: str) -> tuple[AgentResult, Any]:
        """Run agent and compute embedding for the prompt.

        Useful for semantic search/caching of agent responses.

        Args:
            prompt: The user prompt to process

        Returns:
            Tuple of (AgentResult, embedding tensor)
        """
        # Run embedding computation concurrently with agent
        embedding_task = asyncio.create_task(self.compute_service.compute_embedding(prompt))

        result = await self.run(prompt)
        embedding = await embedding_task

        return result, embedding

    def run_sync(self, prompt: str) -> AgentResult:
        """Synchronous wrapper for run().

        Creates a new event loop if needed. Thread-safe for Python 3.15.

        Args:
            prompt: The user prompt to process

        Returns:
            AgentResult with output and metadata
        """
        try:
            loop = asyncio.get_running_loop()
        except RuntimeError:
            loop = None

        if loop is not None:
            # Already in async context - use run_coroutine_threadsafe
            future = asyncio.run_coroutine_threadsafe(self.run(prompt), loop)
            return future.result()

        # No running loop - create one
        return asyncio.run(self.run(prompt))
