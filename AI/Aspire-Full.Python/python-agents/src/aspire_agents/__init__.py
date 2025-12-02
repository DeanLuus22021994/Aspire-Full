"""Aspire Agents - Thread-safe AI agents with Tensor Core compute.

Python 3.15+ free-threaded runtime with GIL disabled (PYTHON_GIL=0).
All agent implementations leverage GPU tensor compute via BatchComputeService.

Key Features:
- True parallelism via Python 3.15 free-threading
- NVIDIA Tensor Core acceleration for embeddings
- Semantic guardrails with GPU-accelerated similarity checks
- Immutable configuration for thread safety

Quick Start:
    >>> from aspire_agents import AgentRunner, AgentConfig, ModelConfig
    >>> config = AgentConfig(model=ModelConfig(name="gpt-4o"))
    >>> runner = AgentRunner(config)
    >>> result = await runner.run("Hello, world!")
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
)
