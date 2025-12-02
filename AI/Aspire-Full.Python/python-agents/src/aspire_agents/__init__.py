"""Aspire Agents - Thread-safe AI agents with Tensor Core compute.

Python 3.15+ free-threaded runtime with GIL disabled (PYTHON_GIL=0).
All agent implementations leverage GPU tensor compute via BatchComputeService.
"""

from importlib import metadata
from typing import Final

# Python 3.15+ thread-safe imports
from .compute import (
    BatchComputeService,
    TensorCoreUnavailableError,
    get_compute_service,
)
from .config import AgentConfig, ModelConfig
from .core import Agent, Runner, function_tool
from .gpu import TensorCoreInfo, ensure_tensor_core_gpu
from .guardrails import (
    GuardrailService,
    ToolGuardrailFunctionOutput,
    ToolInputGuardrailData,
    ToolOutputGuardrailData,
    get_guardrail_service,
    semantic_input_guardrail,
    semantic_output_guardrail,
)
from .runner import AgentResult, AgentRunner

try:
    __version__: Final[str] = metadata.version("aspire-agents")
except metadata.PackageNotFoundError:  # pragma: no cover
    __version__: Final[str] = "0.0.0"

__all__: Final[list[str]] = [
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
    # Tensor compute
    "BatchComputeService",
    "TensorCoreInfo",
    "TensorCoreUnavailableError",
    "get_compute_service",
    "ensure_tensor_core_gpu",
    # Guardrails
    "GuardrailService",
    "ToolGuardrailFunctionOutput",
    "ToolInputGuardrailData",
    "ToolOutputGuardrailData",
    "get_guardrail_service",
    "semantic_input_guardrail",
    "semantic_output_guardrail",
]
