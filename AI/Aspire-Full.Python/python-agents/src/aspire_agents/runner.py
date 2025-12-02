"""Thread-safe agent runner with Tensor Core compute integration.

Python 3.15+ free-threaded runtime (PYTHON_GIL=0) compatible.
All agent executions leverage GPU tensor compute for embeddings and inference.

Architecture:
- AgentResult: Immutable frozen dataclass for thread-safe result handling
- AgentRunner: High-level interface with automatic compute service integration
- Full async/sync support with proper event loop handling

Thread Safety:
- All state is immutable or protected by locks
- Uses BatchComputeService singleton (thread-safe)
- Safe for concurrent use without GIL
"""

from __future__ import annotations

import asyncio
import logging
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any, Final

from .compute import get_compute_service
from .config import AgentConfig, ModelConfig
from .core import Agent, Runner
from .gpu import TensorCoreInfo, ensure_tensor_core_gpu

if TYPE_CHECKING:
    from agents import RunResult

    from .compute import BatchComputeService

logger: Final[logging.Logger] = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class AgentResult:
    """Immutable result from an agent run.

    Thread-safe: frozen dataclass with __slots__ for Python 3.15+ free-threaded.
    All collections are immutable (tuple, frozenset, or dict copy).

    Attributes:
        output: The final output text from the agent
        handoffs: Immutable tuple of agent names that were handed off to
        metadata: Copy of additional metadata from the run
        success: Whether the run completed successfully
        error: Error message if success is False
    """

    output: str
    handoffs: tuple[str, ...] = field(default_factory=tuple)
    metadata: dict[str, Any] = field(default_factory=lambda: dict[str, Any]())
    success: bool = True
    error: str | None = None

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
        handoff_tuple: tuple[str, ...]
        if handoffs is None:
            handoff_tuple = ()
        elif isinstance(handoffs, tuple):
            handoff_tuple = handoffs
        else:
            handoff_tuple = tuple(handoffs)

        return cls(
            output=str(result.final_output) if result.final_output else "",
            handoffs=handoff_tuple,
            metadata={
                "last_agent": result.last_agent.name if result.last_agent else None,
            },
            success=True,
            error=None,
        )

    @classmethod
    def from_error(cls, error: Exception, context: str = "") -> AgentResult:
        """Create an error result.

        Args:
            error: The exception that occurred
            context: Additional context about where the error happened

        Returns:
            AgentResult with success=False and error details
        """
        error_msg = f"{context}: {error}" if context else str(error)
        return cls(
            output="",
            handoffs=(),
            metadata={"error_type": type(error).__name__},
            success=False,
            error=error_msg,
        )


class AgentRunner:
    """Thread-safe agent runner with integrated Tensor Core compute.

    Provides a high-level interface for running agents with:
    - Automatic GPU tensor compute for embeddings
    - Thread-safe execution via Python 3.15 free-threading
    - Configurable agent behavior via AgentConfig
    - Both async and sync execution modes

    Attributes:
        config: Agent configuration (frozen dataclass)
        compute_service: BatchComputeService singleton for GPU compute

    Examples:
        >>> runner = AgentRunner()
        >>> result = await runner.run("Hello, world!")
        >>> print(result.output)

        >>> runner = AgentRunner(AgentConfig(name="coder", ...))
        >>> result = runner.run_sync("Write a function")
    """

    __slots__ = ("config", "compute_service", "_agent", "_runner", "_tensor_info")

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
        # Build config with proper ModelConfig if not provided
        if config is not None:
            self.config = config
        else:
            super().__init__()
            self.config = AgentConfig(
                name="default-agent",
                description="Default agent configuration",
                prompt="You are a helpful assistant.",
                model=ModelConfig(name="gpt-4o-mini"),
            )

        self.compute_service: BatchComputeService = compute_service or get_compute_service()

        # Initialize agent and runner (lazy - created on first run)
        self._agent: Agent | None = None
        self._runner: Runner | None = None
        self._tensor_info: TensorCoreInfo | None = None

        logger.info(
            "AgentRunner initialized with model=%s, compute_service=%s",
            self.config.model.name,
            type(self.compute_service).__name__,
        )

    @property
    def tensor_info(self) -> TensorCoreInfo:
        """Get tensor core GPU info.

        Returns:
            TensorCoreInfo with device metadata
        """
        if self._tensor_info is None:
            self._tensor_info = ensure_tensor_core_gpu()
        return self._tensor_info

    def _ensure_agent(self) -> Agent:
        """Ensure agent is initialized (lazy initialization).

        Returns:
            Initialized Agent instance
        """
        if self._agent is None:
            self._agent = Agent(
                name=self.config.name,
                instructions=self.config.prompt,
                model=self.config.model.name,
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

        logger.debug(
            "Running agent '%s' with prompt: %s...",
            agent.name,
            prompt[:50] if len(prompt) > 50 else prompt,
        )

        try:
            result = await Runner.run(agent, prompt)

            # Extract handoffs as tuple for immutability
            handoffs: tuple[str, ...] = ()
            result_handoffs = getattr(result, "handoffs", None)
            if result_handoffs:
                handoffs = tuple(str(h) for h in result_handoffs)

            return AgentResult.from_run_result(result, handoffs)
        except Exception as e:
            logger.error("Agent run failed: %s", e)
            return AgentResult.from_error(e, f"Agent '{agent.name}' failed")

    async def arun(self, prompt: str) -> AgentResult:
        """Alias for run() for CLI compatibility.

        Args:
            prompt: The user prompt to process

        Returns:
            AgentResult with output and metadata
        """
        return await self.run(prompt)

    async def run_with_embedding(self, prompt: str) -> tuple[AgentResult, Any]:
        """Run agent and compute embedding for the prompt.

        Useful for semantic search/caching of agent responses.
        Embedding is computed concurrently with agent execution.

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

    async def run_batch(self, prompts: list[str]) -> list[AgentResult]:
        """Run multiple prompts concurrently.

        Leverages Python 3.15 free-threading for true parallelism.

        Args:
            prompts: List of prompts to process

        Returns:
            List of AgentResults in same order as prompts
        """
        tasks = [self.run(prompt) for prompt in prompts]
        return await asyncio.gather(*tasks)

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

    def pretty_print(self, result: AgentResult) -> None:
        """Pretty print the agent result to console.

        Args:
            result: The AgentResult to display
        """
        if result.success:
            print(f"\n✅ Output:\n{result.output}")
            if result.handoffs:
                print(f"\n🔄 Handoffs: {', '.join(result.handoffs)}")
            if result.metadata:
                print(f"\n📊 Metadata: {result.metadata}")
        else:
            print(f"\n❌ Error: {result.error}")

    def get_compute_stats(self) -> dict[str, int | float]:
        """Get compute service statistics.

        Returns:
            Dictionary with total_requests, total_batches, avg_batch_size, queue_size
        """
        return self.compute_service.get_stats()

    def get_gpu_info(self) -> dict[str, Any]:
        """Get GPU information for diagnostics.

        Returns:
            Dictionary with GPU name, compute capability, memory, etc.
        """
        info = self.tensor_info
        return {
            "name": info.name,
            "compute_capability": info.compute_capability,
            "total_memory_gb": info.total_memory_gb,
            "supports_fp16": info.supports_fp16,
            "supports_bf16": info.supports_bf16,
            "supports_fp8": info.supports_fp8,
            "tf32_enabled": info.tf32_enabled,
            "gil_disabled": info.gil_disabled,
            "tensor_core_generation": info.tensor_core_generation,
        }
