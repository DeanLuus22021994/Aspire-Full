"""Aspire Agents - Thread-safe AI agents with Tensor Core compute.

Python 3.15+ free-threaded runtime with GIL disabled (PYTHON_GIL=0).
All agent implementations leverage GPU tensor compute via BatchComputeService.

Key Features:
- True parallelism via Python 3.15 free-threading
- NVIDIA Tensor Core acceleration for embeddings
- Semantic guardrails with GPU-accelerated similarity checks
- Immutable configuration for thread safety
- Sub-Agent orchestration with configurable concurrency

Environment Variables (from Dockerfile):
- ASPIRE_AGENT_THREAD_POOL_SIZE: Thread pool size (default: 8)
- ASPIRE_SUBAGENT_MAX_CONCURRENT: Max concurrent sub-agents (default: 16)
- ASPIRE_TENSOR_BATCH_SIZE: Batch size for tensor ops (default: 32)
- ASPIRE_TENSOR_OFFLOAD_ENABLED: Enable tensor offloading (default: 1)
- ASPIRE_SUBAGENT_GPU_SHARE: Enable GPU sharing (default: 1)
- ASPIRE_COMPUTE_MODE: Compute mode - gpu|cpu|hybrid (default: gpu)
- CUDA_TENSOR_CORE_ALIGNMENT: Memory alignment in bytes (default: 128)

Quick Start:
    >>> from aspire_agents import AgentRunner, AgentConfig, ModelConfig
    >>> config = AgentConfig(model=ModelConfig(name="gpt-4o"))
    >>> runner = AgentRunner(config)
    >>> result = await runner.run("Hello, world!")

Sub-Agent Orchestration:
    >>> from aspire_agents import get_orchestrator, SubAgentConfig
    >>> orchestrator = get_orchestrator()
    >>> orchestrator.register_agent("translator", translator_agent)
    >>> results = await orchestrator.execute_parallel([
    ...     ("translator", "Translate to Spanish"),
    ...     ("translator", "Translate to French"),
    ... ])
"""

from __future__ import annotations

from importlib import metadata
from typing import Final

# Python 3.15+ thread-safe imports - all using frozen dataclasses
from .compute import (
    BatchComputeService,
    ComputeConfig,
    TensorCoreUnavailableError,
    get_compute_service,
    reset_compute_service,
)
from .config import AgentConfig, ModelConfig, TensorConfig
from .core import Agent, Runner, function_tool
from .gpu import (
    TensorCoreInfo,
    empty_cache,
    ensure_tensor_core_gpu,
    get_gpu_memory_info,
    synchronize_cuda,
)
from .guardrails import (
    GuardrailService,
    ToolGuardrailFunctionOutput,
    ToolInputGuardrailData,
    ToolOutputGuardrailData,
    get_guardrail_service,
    reset_guardrail_service,
    semantic_input_guardrail,
    semantic_output_guardrail,
)
from .kernel import (
    SemanticKernelUnavailableError,
    build_kernel,
    build_kernel_with_plugins,
)
from .runner import AgentResult, AgentRunner
from .subagent import (
    ASPIRE_AGENT_THREAD_POOL_SIZE,
    ASPIRE_COMPUTE_MODE,
    ASPIRE_SUBAGENT_GPU_SHARE,
    ASPIRE_SUBAGENT_MAX_CONCURRENT,
    ASPIRE_TENSOR_BATCH_SIZE,
    ASPIRE_TENSOR_OFFLOAD_ENABLED,
    CUDA_TENSOR_CORE_ALIGNMENT,
    SubAgentConfig,
    SubAgentOrchestrator,
    SubAgentResult,
    get_orchestrator,
    reset_orchestrator,
)


def _get_version() -> str:
    """Get package version with fallback for development installs."""
    try:
        return metadata.version("aspire-agents")
    except metadata.PackageNotFoundError:
        return "0.0.0-dev"


# Module version - computed once at import time
__version__: Final[str] = _get_version()

# Public API exports
__all__: Final[tuple[str, ...]] = (
    # Version
    "__version__",
    # Core agents
    "Agent",
    "Runner",
    "AgentRunner",
    "AgentResult",
    "function_tool",
    # Configuration
    "AgentConfig",
    "ModelConfig",
    "TensorConfig",
    # Tensor compute
    "BatchComputeService",
    "ComputeConfig",
    "TensorCoreInfo",
    "TensorCoreUnavailableError",
    "get_compute_service",
    "reset_compute_service",
    "ensure_tensor_core_gpu",
    "get_gpu_memory_info",
    "synchronize_cuda",
    "empty_cache",
    # Guardrails
    "GuardrailService",
    "ToolGuardrailFunctionOutput",
    "ToolInputGuardrailData",
    "ToolOutputGuardrailData",
    "get_guardrail_service",
    "reset_guardrail_service",
    "semantic_input_guardrail",
    "semantic_output_guardrail",
    # Semantic Kernel
    "build_kernel",
    "build_kernel_with_plugins",
    "SemanticKernelUnavailableError",
    # Sub-Agent Orchestration
    "SubAgentConfig",
    "SubAgentResult",
    "SubAgentOrchestrator",
    "get_orchestrator",
    "reset_orchestrator",
    # Environment Constants
    "ASPIRE_AGENT_THREAD_POOL_SIZE",
    "ASPIRE_SUBAGENT_MAX_CONCURRENT",
    "ASPIRE_TENSOR_BATCH_SIZE",
    "ASPIRE_TENSOR_OFFLOAD_ENABLED",
    "ASPIRE_SUBAGENT_GPU_SHARE",
    "ASPIRE_COMPUTE_MODE",
    "CUDA_TENSOR_CORE_ALIGNMENT",
)
