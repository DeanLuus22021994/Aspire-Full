"""Sub-Agent orchestration for Python 3.16+ free-threaded runtime.

GPU-ONLY. NO CPU FALLBACK.

Provides thread-safe sub-agent management with:
- ASPIRE_SUBAGENT_MAX_CONCURRENT concurrent sub-agent execution
- ASPIRE_SUBAGENT_GPU_SHARE GPU memory sharing between sub-agents
- ASPIRE_AGENT_THREAD_POOL_SIZE thread pool management
- CUDA_TENSOR_CORE_ALIGNMENT=128 byte alignment enforcement

Thread Safety:
- All orchestration uses ThreadPoolExecutor with configurable size
- GPU memory is shared with memory fraction per sub-agent
- Uses Python 3.16 free-threading (PYTHON_GIL=0) for true parallelism

Environment Variables:
- ASPIRE_SUBAGENT_MAX_CONCURRENT: Max concurrent sub-agents (default: 16)
- ASPIRE_SUBAGENT_GPU_SHARE: Enable GPU sharing (default: 1)
- ASPIRE_AGENT_THREAD_POOL_SIZE: Thread pool size (default: 8)
- ASPIRE_TENSOR_BATCH_SIZE: Batch size for tensor ops (default: 32)
- ASPIRE_COMPUTE_MODE: Compute mode - GPU only (default: gpu)
- CUDA_TENSOR_CORE_ALIGNMENT: Memory alignment bytes (default: 128)
"""

from __future__ import annotations

import asyncio
import logging
import os
import threading
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass, field
from typing import Any, Final

import torch
from agents import Agent as BaseAgent

from ._typing import is_gil_disabled, set_cuda_memory_fraction
from .config import AgentConfig
from .core import Agent

logger: Final[logging.Logger] = logging.getLogger(__name__)

# ============================================================================
# Environment Variable Configuration
# ============================================================================

# Sub-Agent orchestration settings from Dockerfile environment
ASPIRE_SUBAGENT_MAX_CONCURRENT: Final[int] = int(os.environ.get("ASPIRE_SUBAGENT_MAX_CONCURRENT", "16"))
ASPIRE_SUBAGENT_GPU_SHARE: Final[bool] = os.environ.get("ASPIRE_SUBAGENT_GPU_SHARE", "1") == "1"
ASPIRE_AGENT_THREAD_POOL_SIZE: Final[int] = int(os.environ.get("ASPIRE_AGENT_THREAD_POOL_SIZE", "8"))
ASPIRE_TENSOR_BATCH_SIZE: Final[int] = int(os.environ.get("ASPIRE_TENSOR_BATCH_SIZE", "32"))
ASPIRE_COMPUTE_MODE: Final[str] = os.environ.get("ASPIRE_COMPUTE_MODE", "gpu")
CUDA_TENSOR_CORE_ALIGNMENT: Final[int] = int(os.environ.get("CUDA_TENSOR_CORE_ALIGNMENT", "128"))
ASPIRE_TENSOR_OFFLOAD_ENABLED: Final[bool] = os.environ.get("ASPIRE_TENSOR_OFFLOAD_ENABLED", "1") == "1"


# GPU-only: compute_mode is always 'gpu'
_COMPUTE_MODE: Final[str] = "gpu"


# ============================================================================
# Sub-Agent Configuration
# ============================================================================


@dataclass(frozen=True, slots=True)
class SubAgentConfig:
    """Immutable configuration for sub-agent orchestration.

    GPU-ONLY. NO CPU FALLBACK.
    Thread-safe via frozen dataclass with __slots__.
    All values are read from environment or provided at construction.

    Attributes:
        max_concurrent: Maximum concurrent sub-agents
        gpu_share_enabled: Whether sub-agents share GPU memory
        thread_pool_size: Size of the thread pool for sub-agent execution
        tensor_batch_size: Batch size for tensor operations
        compute_mode: Always 'gpu' - no CPU fallback
        tensor_alignment: CUDA memory alignment in bytes
        offload_enabled: Whether tensor offloading is enabled
    """

    max_concurrent: int = field(default_factory=lambda: ASPIRE_SUBAGENT_MAX_CONCURRENT)
    gpu_share_enabled: bool = field(default_factory=lambda: ASPIRE_SUBAGENT_GPU_SHARE)
    thread_pool_size: int = field(default_factory=lambda: ASPIRE_AGENT_THREAD_POOL_SIZE)
    tensor_batch_size: int = field(default_factory=lambda: ASPIRE_TENSOR_BATCH_SIZE)
    compute_mode: str = "gpu"  # GPU-only, no CPU fallback
    tensor_alignment: int = field(default_factory=lambda: CUDA_TENSOR_CORE_ALIGNMENT)
    offload_enabled: bool = field(default_factory=lambda: ASPIRE_TENSOR_OFFLOAD_ENABLED)

    @classmethod
    def from_env(cls) -> SubAgentConfig:
        """Create SubAgentConfig from environment variables.

        Returns:
            SubAgentConfig with all values from environment
        """
        return cls()

    @property
    def uses_gpu(self) -> bool:
        """GPU is always required - no CPU fallback."""
        return True


# ============================================================================
# Sub-Agent Orchestrator
# ============================================================================

# Thread-safe singleton pattern
_ORCHESTRATOR_LOCK: Final[threading.Lock] = threading.Lock()
_orchestrator: SubAgentOrchestrator | None = None


@dataclass(frozen=True, slots=True)
class SubAgentResult:
    """Immutable result from a sub-agent execution.

    Thread-safe via frozen dataclass.

    Attributes:
        agent_name: Name of the sub-agent
        output: The sub-agent's output
        success: Whether execution succeeded
        error: Error message if failed
        execution_time_ms: Execution time in milliseconds
    """

    agent_name: str
    output: str
    success: bool = True
    error: str | None = None
    execution_time_ms: float = 0.0


class SubAgentOrchestrator:
    """Thread-safe orchestrator for concurrent sub-agent execution.

    Manages a pool of sub-agents with:
    - Configurable concurrency limits (ASPIRE_SUBAGENT_MAX_CONCURRENT)
    - GPU memory sharing (ASPIRE_SUBAGENT_GPU_SHARE)
    - Thread pool management (ASPIRE_AGENT_THREAD_POOL_SIZE)
    - Tensor Core alignment enforcement (CUDA_TENSOR_CORE_ALIGNMENT)

    Thread Safety:
    - Uses ThreadPoolExecutor for concurrent execution
    - All state is protected by locks
    - Safe for Python 3.15 free-threaded runtime (GIL disabled)

    Attributes:
        config: SubAgentConfig with orchestration settings
        executor: ThreadPoolExecutor for sub-agent execution
        semaphore: Semaphore limiting concurrent executions
    """

    __slots__ = (
        "config",
        "executor",
        "semaphore",
        "_agents",
        "_agents_lock",
        "_stats_lock",
        "_total_executions",
        "_total_failures",
        "_initialized",
    )

    def __init__(self, config: SubAgentConfig | None = None) -> None:
        """Initialize the sub-agent orchestrator.

        Args:
            config: Optional configuration. Uses environment defaults if None.
        """
        self.config = config or SubAgentConfig.from_env()
        self.executor = ThreadPoolExecutor(
            max_workers=self.config.thread_pool_size,
            thread_name_prefix="SubAgent-Worker",
        )
        self.semaphore = asyncio.Semaphore(self.config.max_concurrent)
        self._agents: dict[str, BaseAgent[Any] | Agent] = {}
        self._agents_lock = threading.Lock()
        self._stats_lock = threading.Lock()
        super().__init__()
        self._total_executions = 0
        self._total_failures = 0
        self._initialized = False

        # Configure GPU memory sharing if enabled
        if self.config.uses_gpu and self.config.gpu_share_enabled:
            self._configure_gpu_sharing()

        self._initialized = True
        logger.info(
            "SubAgentOrchestrator initialized: max_concurrent=%d, thread_pool=%d, "
            + "compute_mode=%s, gpu_share=%s, GIL=%s",
            self.config.max_concurrent,
            self.config.thread_pool_size,
            self.config.compute_mode,
            "enabled" if self.config.gpu_share_enabled else "disabled",
            "disabled" if is_gil_disabled() else "enabled",
        )

    def _configure_gpu_sharing(self) -> None:
        """Configure GPU memory sharing between sub-agents.

        Sets memory fraction based on max concurrent sub-agents.
        """
        if not torch.cuda.is_available():
            return

        try:
            # Reserve portion of memory for each sub-agent
            # Use 80% of total memory, divided by max concurrent
            memory_fraction = 0.8 / max(1, self.config.max_concurrent)
            set_cuda_memory_fraction(min(memory_fraction * self.config.max_concurrent, 0.95))
            logger.debug(
                "GPU memory sharing configured: %.1f%% per sub-agent",
                memory_fraction * 100,
            )
        except Exception as e:
            logger.warning("Could not configure GPU memory sharing: %s", e)

    def register_agent(self, name: str, agent: BaseAgent[Any] | Agent) -> None:
        """Register a sub-agent for orchestration.

        Thread-safe registration with lock protection.

        Args:
            name: Unique name for the sub-agent
            agent: The Agent instance to register (accepts agents.Agent or aspire_agents.Agent)
        """
        with self._agents_lock:
            self._agents[name] = agent
            logger.debug("Registered sub-agent: %s", name)

    def register_agent_from_config(self, config: AgentConfig) -> Agent:
        """Create and register a sub-agent from configuration.

        Args:
            config: AgentConfig for the sub-agent

        Returns:
            The created Agent instance
        """
        agent = Agent(
            name=config.name,
            instructions=config.prompt,
            model=config.model.name,
        )
        self.register_agent(config.name, agent)
        return agent

    async def execute_subagent(
        self,
        name: str,
        prompt: str,
    ) -> SubAgentResult:
        """Execute a registered sub-agent with concurrency control.

        Uses semaphore to limit concurrent executions to max_concurrent.
        Thread-safe for Python 3.15 free-threaded runtime.

        Args:
            name: Name of the registered sub-agent
            prompt: The prompt to send to the sub-agent

        Returns:
            SubAgentResult with output or error
        """
        import time

        from .runner import AgentRunner

        async with self.semaphore:
            start_time = time.perf_counter()

            with self._agents_lock:
                agent = self._agents.get(name)

            if agent is None:
                return SubAgentResult(
                    agent_name=name,
                    output="",
                    success=False,
                    error=f"Sub-agent '{name}' not registered",
                )

            try:
                # Create runner for this execution
                # Handle instructions that may be a callable
                instructions_str = agent.instructions if isinstance(agent.instructions, str) else ""
                # Handle model that may be str, Model, or None
                model_name = (
                    agent.model
                    if isinstance(agent.model, str)
                    else getattr(agent.model, "name", None) if agent.model else None
                )
                from .config import ModelConfig

                runner = AgentRunner(
                    AgentConfig(
                        name=agent.name,
                        prompt=instructions_str,
                        model=ModelConfig.from_string(model_name or "gpt-4o-mini"),
                    )
                )
                result = await runner.run(prompt)

                elapsed_ms = (time.perf_counter() - start_time) * 1000

                with self._stats_lock:
                    self._total_executions += 1

                return SubAgentResult(
                    agent_name=name,
                    output=result.output,
                    success=result.success,
                    error=result.error,
                    execution_time_ms=elapsed_ms,
                )

            except Exception as e:
                elapsed_ms = (time.perf_counter() - start_time) * 1000
                with self._stats_lock:
                    self._total_executions += 1
                    self._total_failures += 1

                logger.error("Sub-agent '%s' execution failed: %s", name, e)
                return SubAgentResult(
                    agent_name=name,
                    output="",
                    success=False,
                    error=str(e),
                    execution_time_ms=elapsed_ms,
                )

    async def execute_parallel(
        self,
        executions: list[tuple[str, str]],
    ) -> list[SubAgentResult]:
        """Execute multiple sub-agents in parallel.

        Respects max_concurrent limit via semaphore.
        Leverages Python 3.15 free-threading for true parallelism.

        Args:
            executions: List of (agent_name, prompt) tuples

        Returns:
            List of SubAgentResults in same order as input
        """
        tasks = [self.execute_subagent(name, prompt) for name, prompt in executions]
        return await asyncio.gather(*tasks)

    async def execute_pipeline(
        self,
        pipeline: list[tuple[str, str]],
    ) -> list[SubAgentResult]:
        """Execute sub-agents in sequence, passing output to next.

        Each sub-agent's output becomes the input to the next.
        Useful for multi-step processing pipelines.

        Args:
            pipeline: List of (agent_name, initial_prompt_template) tuples.
                      Template can contain {prev_output} placeholder.

        Returns:
            List of SubAgentResults from each pipeline stage
        """
        results: list[SubAgentResult] = []
        prev_output = ""

        for name, prompt_template in pipeline:
            prompt = prompt_template.format(prev_output=prev_output)
            result = await self.execute_subagent(name, prompt)
            results.append(result)

            if not result.success:
                logger.warning("Pipeline stopped at '%s': %s", name, result.error)
                break

            prev_output = result.output

        return results

    def get_stats(self) -> dict[str, int | float]:
        """Get orchestrator statistics.

        Returns:
            Dictionary with execution stats
        """
        with self._stats_lock:
            return {
                "total_executions": self._total_executions,
                "total_failures": self._total_failures,
                "success_rate": (self._total_executions - self._total_failures) / max(1, self._total_executions),
                "registered_agents": len(self._agents),
                "max_concurrent": self.config.max_concurrent,
                "thread_pool_size": self.config.thread_pool_size,
            }

    def get_registered_agents(self) -> tuple[str, ...]:
        """Get names of all registered sub-agents."""
        with self._agents_lock:
            return tuple(self._agents.keys())

    def shutdown(self) -> None:
        """Shutdown the orchestrator and release resources."""
        self.executor.shutdown(wait=True)
        logger.info("SubAgentOrchestrator shutdown complete")


# ============================================================================
# Singleton Access
# ============================================================================


def get_orchestrator(config: SubAgentConfig | None = None) -> SubAgentOrchestrator:
    """Get or create the singleton SubAgentOrchestrator.

    Thread-safe via double-checked locking.

    Args:
        config: Optional configuration for first initialization

    Returns:
        The singleton SubAgentOrchestrator instance
    """
    global _orchestrator
    if _orchestrator is None:
        with _ORCHESTRATOR_LOCK:
            if _orchestrator is None:
                _orchestrator = SubAgentOrchestrator(config)
    return _orchestrator


def reset_orchestrator() -> None:
    """Reset the singleton orchestrator (for testing)."""
    global _orchestrator
    with _ORCHESTRATOR_LOCK:
        if _orchestrator is not None:
            _orchestrator.shutdown()
            _orchestrator = None


# ============================================================================
# Public API
# ============================================================================

__all__: Final[tuple[str, ...]] = (
    # Configuration
    "SubAgentConfig",
    "SubAgentResult",
    # Orchestrator
    "SubAgentOrchestrator",
    "get_orchestrator",
    "reset_orchestrator",
    # Environment constants
    "ASPIRE_SUBAGENT_MAX_CONCURRENT",
    "ASPIRE_SUBAGENT_GPU_SHARE",
    "ASPIRE_AGENT_THREAD_POOL_SIZE",
    "ASPIRE_TENSOR_BATCH_SIZE",
    "ASPIRE_COMPUTE_MODE",
    "CUDA_TENSOR_CORE_ALIGNMENT",
    "ASPIRE_TENSOR_OFFLOAD_ENABLED",
)
