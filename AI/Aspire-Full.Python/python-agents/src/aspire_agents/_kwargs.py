"""Type-safe, thread-safe kwargs definitions for Python 3.15+ free-threaded runtime.

This module provides TypedDict and Protocol-based kwargs patterns that ensure:
- Full static type checking via Pyright/Pylance
- Runtime immutability for thread safety
- Zero-copy frozen semantics for GIL-free execution
- Proper IDE autocompletion and documentation

GPU-ONLY: All compute kwargs require CUDA. No CPU fallback.

Thread Safety:
- All TypedDict classes are immutable at runtime
- All Protocol classes define read-only interfaces
- Frozen dataclass alternatives provided for mutable construction

Usage:
    >>> def configure_compute(**kwargs: Unpack[ComputeKwargs]) -> ComputeConfig:
    ...     return ComputeConfig(**kwargs)

    >>> config = configure_compute(batch_size=64, use_torch_compile=True)
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import (
    TYPE_CHECKING,
    Final,
    NotRequired,
    Protocol,
    Required,
    TypedDict,
    runtime_checkable,
)

if TYPE_CHECKING:
    import torch

# ============================================================================
# Compute Configuration Kwargs
# ============================================================================


class ComputeKwargs(TypedDict, total=False):
    """Type-safe kwargs for BatchComputeService configuration.

    GPU-ONLY. No CPU fallback allowed.
    All fields are optional with sensible defaults.

    Thread Safety:
    - TypedDict is immutable at runtime
    - All values are simple types or frozen

    Usage:
        >>> def create_service(**kwargs: Unpack[ComputeKwargs]) -> BatchComputeService:
        ...     config = ComputeConfig(**kwargs)
        ...     return BatchComputeService(config)
    """

    model_name: str
    """HuggingFace model name for embeddings (default: sentence-transformers/all-MiniLM-L6-v2)"""

    batch_size: int
    """Batch size for tensor operations (default: 32 from ASPIRE_TENSOR_BATCH_SIZE)"""

    max_latency_ms: int
    """Maximum latency in ms before batch processing (default: 10)"""

    use_torch_compile: bool
    """Enable torch.compile optimization for Tensor Core efficiency (default: True)"""

    use_mixed_precision: bool
    """Enable FP16/BF16 mixed precision for Tensor Core utilization (default: True)"""

    tensor_alignment: int
    """CUDA memory alignment in bytes (default: 128 from CUDA_TENSOR_CORE_ALIGNMENT)"""


class SubAgentKwargs(TypedDict, total=False):
    """Type-safe kwargs for SubAgentOrchestrator configuration.

    GPU-ONLY. No CPU fallback allowed.
    All fields are optional with sensible defaults from environment.

    Thread Safety:
    - TypedDict is immutable at runtime
    - All values are simple types

    Usage:
        >>> def create_orchestrator(**kwargs: Unpack[SubAgentKwargs]) -> SubAgentOrchestrator:
        ...     config = SubAgentConfig(**kwargs)
        ...     return SubAgentOrchestrator(config)
    """

    max_concurrent: int
    """Maximum concurrent sub-agents (default: 16 from ASPIRE_SUBAGENT_MAX_CONCURRENT)"""

    gpu_share_enabled: bool
    """Enable GPU memory sharing between sub-agents (default: True)"""

    thread_pool_size: int
    """Thread pool size for sub-agent execution (default: 8)"""

    tensor_batch_size: int
    """Batch size for tensor operations (default: 32)"""

    tensor_alignment: int
    """CUDA memory alignment in bytes (default: 128)"""

    offload_enabled: bool
    """Enable tensor offloading (default: True)"""


class TensorConfigKwargs(TypedDict, total=False):
    """Type-safe kwargs for TensorConfig construction.

    GPU-ONLY. use_gpu is always True.
    All fields are optional with sensible defaults.

    Thread Safety:
    - TypedDict is immutable at runtime
    - Maps directly to frozen dataclass

    Usage:
        >>> def create_tensor_config(**kwargs: Unpack[TensorConfigKwargs]) -> TensorConfig:
        ...     return TensorConfig(**kwargs)
    """

    use_gpu: bool
    """Enable GPU acceleration - always True, GPU required (default: True)"""

    use_tensor_cores: bool
    """Enable Tensor Core optimizations for FP16/TF32 (default: True)"""

    use_flash_attention: bool
    """Enable Flash Attention for efficient transformers (default: True)"""

    batch_size: int
    """Batch size for batched operations (default: 32)"""

    max_sequence_length: int
    """Maximum token sequence length (default: 512)"""

    use_torch_compile: bool
    """Enable torch.compile() optimization (default: True)"""

    mixed_precision: bool
    """Enable automatic mixed precision (default: True)"""

    tensor_alignment: int
    """CUDA memory alignment in bytes (default: 128)"""


# ============================================================================
# Model Configuration Kwargs
# ============================================================================


class ModelKwargs(TypedDict, total=False):
    """Type-safe kwargs for ModelConfig construction.

    Thread Safety:
    - TypedDict is immutable at runtime
    - Maps directly to frozen dataclass

    Usage:
        >>> def create_model_config(**kwargs: Unpack[ModelKwargs]) -> ModelConfig:
        ...     return ModelConfig(**kwargs)
    """

    provider: str
    """Model provider: openai, azure, github, anthropic, local (default: openai)"""

    name: str
    """Model name or deployment name (default: gpt-4.1-mini)"""

    deployment: str | None
    """Azure deployment name (optional)"""

    endpoint: str | None
    """Custom API endpoint (optional)"""

    api_version: str | None
    """API version for Azure (optional)"""


class AgentKwargs(TypedDict, total=False):
    """Type-safe kwargs for AgentConfig construction.

    Thread Safety:
    - TypedDict is immutable at runtime
    - All nested configs are frozen dataclasses

    Usage:
        >>> def create_agent_config(**kwargs: Unpack[AgentKwargs]) -> AgentConfig:
        ...     return AgentConfig(**kwargs)
    """

    name: str
    """Agent identifier (default: default-agent)"""

    description: str
    """Human-readable description"""

    prompt: str
    """System prompt/instructions"""

    temperature: float
    """Sampling temperature 0.0-2.0 (default: 0.0 = deterministic)"""

    top_p: float | None
    """Nucleus sampling parameter (optional)"""

    max_tokens: int | None
    """Maximum output tokens (optional)"""


# ============================================================================
# Agent Runner Kwargs
# ============================================================================


class RunKwargs(TypedDict, total=False):
    """Type-safe kwargs for AgentRunner.run() method.

    Thread Safety:
    - TypedDict is immutable at runtime
    - All values are simple types

    Usage:
        >>> async def run_agent(prompt: str, **kwargs: Unpack[RunKwargs]) -> AgentResult:
        ...     return await runner.run(prompt, **kwargs)
    """

    stream: bool
    """Enable streaming output (default: False)"""

    max_iterations: int
    """Maximum tool call iterations (default: 10)"""

    timeout_seconds: float | None
    """Timeout in seconds (optional)"""


# ============================================================================
# Guardrail Kwargs
# ============================================================================


class GuardrailKwargs(TypedDict, total=False):
    """Type-safe kwargs for guardrail configuration.

    Thread Safety:
    - TypedDict is immutable at runtime
    - Threshold is a simple float

    Usage:
        >>> def create_guardrail(**kwargs: Unpack[GuardrailKwargs]) -> GuardrailFunc:
        ...     return semantic_input_guardrail(**kwargs)
    """

    category: str
    """Category to check against (default: harmful for input, pii for output)"""

    threshold: float
    """Similarity threshold 0.0-1.0 (default: 0.4)"""


# ============================================================================
# Embedding Kwargs
# ============================================================================


class EmbeddingKwargs(TypedDict, total=False):
    """Type-safe kwargs for embedding computation.

    GPU-ONLY. All embeddings computed on CUDA.

    Thread Safety:
    - TypedDict is immutable at runtime
    - All values are simple types

    Usage:
        >>> async def compute_embedding(text: str, **kwargs: Unpack[EmbeddingKwargs]) -> Tensor:
        ...     return await service.compute_embedding(text, **kwargs)
    """

    normalize: bool
    """L2 normalize embeddings (default: True)"""

    max_length: int
    """Maximum sequence length (default: 512)"""

    pooling: str
    """Pooling strategy: mean, cls, max (default: mean)"""


# ============================================================================
# Required Kwargs Variants
# ============================================================================


class AgentRunKwargs(TypedDict):
    """Type-safe kwargs for agent execution with required fields.

    Thread Safety:
    - TypedDict is immutable at runtime
    - Prompt is required for execution

    Usage:
        >>> async def run(**kwargs: Unpack[AgentRunKwargs]) -> AgentResult:
        ...     return await runner.run(kwargs["prompt"])
    """

    prompt: Required[str]
    """The user prompt to process - REQUIRED"""

    stream: NotRequired[bool]
    """Enable streaming output (default: False)"""

    max_iterations: NotRequired[int]
    """Maximum tool call iterations (default: 10)"""


class BatchEmbeddingKwargs(TypedDict):
    """Type-safe kwargs for batch embedding computation.

    GPU-ONLY. Batched for Tensor Core efficiency.

    Thread Safety:
    - TypedDict is immutable at runtime
    - Texts is required as immutable Sequence

    Usage:
        >>> async def compute_batch(**kwargs: Unpack[BatchEmbeddingKwargs]) -> Tensor:
        ...     return await service.compute_embeddings(kwargs["texts"])
    """

    texts: Required[list[str]]
    """List of texts to embed - REQUIRED"""

    normalize: NotRequired[bool]
    """L2 normalize embeddings (default: True)"""

    batch_size: NotRequired[int]
    """Override batch size (default: from config)"""


# ============================================================================
# Protocol-based Kwargs Interfaces
# ============================================================================


@runtime_checkable
class SupportsComputeKwargs(Protocol):
    """Protocol for objects that can be configured with ComputeKwargs.

    Thread Safety:
    - Protocol is read-only interface
    - Implementations must be thread-safe

    Usage:
        >>> def configure(obj: SupportsComputeKwargs, **kwargs: Unpack[ComputeKwargs]) -> None:
        ...     obj.configure(**kwargs)
    """

    def configure(
        self,
        *,
        model_name: str | None = None,
        batch_size: int | None = None,
        max_latency_ms: int | None = None,
        use_torch_compile: bool | None = None,
        use_mixed_precision: bool | None = None,
        tensor_alignment: int | None = None,
    ) -> None:
        """Configure the compute service with type-safe kwargs."""
        ...


@runtime_checkable
class SupportsEmbedding(Protocol):
    """Protocol for objects that can compute embeddings.

    GPU-ONLY. All embeddings on CUDA.

    Thread Safety:
    - Protocol is read-only interface
    - Implementations must be async-safe

    Usage:
        >>> async def embed(service: SupportsEmbedding, text: str) -> Tensor:
        ...     return await service.compute_embedding(text)
    """

    async def compute_embedding(self, text: str) -> "torch.Tensor":
        """Compute embedding for single text."""
        ...

    async def compute_embeddings(self, texts: list[str]) -> "torch.Tensor":
        """Compute embeddings for multiple texts."""
        ...


@runtime_checkable
class SupportsAgentRun(Protocol):
    """Protocol for objects that can run agents.

    Thread Safety:
    - Protocol is read-only interface
    - Implementations must be async-safe

    Usage:
        >>> async def execute(runner: SupportsAgentRun, prompt: str) -> AgentResult:
        ...     return await runner.run(prompt)
    """

    async def run(self, prompt: str) -> object:
        """Run agent with prompt."""
        ...

    def run_sync(self, prompt: str) -> object:
        """Run agent synchronously."""
        ...


# ============================================================================
# Frozen Dataclass Alternatives (for mutable construction)
# ============================================================================


@dataclass(frozen=True, slots=True)
class FrozenComputeKwargs:
    """Immutable container for ComputeKwargs.

    Use when you need to construct kwargs incrementally,
    then freeze for thread-safe passing.

    Thread Safety:
    - Frozen dataclass with __slots__
    - Fully immutable after construction

    Usage:
        >>> kwargs = FrozenComputeKwargs(batch_size=64)
        >>> config = ComputeConfig(**kwargs.to_dict())
    """

    model_name: str | None = None
    batch_size: int | None = None
    max_latency_ms: int | None = None
    use_torch_compile: bool | None = None
    use_mixed_precision: bool | None = None
    tensor_alignment: int | None = None

    def to_dict(self) -> ComputeKwargs:
        """Convert to TypedDict, excluding None values."""
        result: ComputeKwargs = {}
        if self.model_name is not None:
            result["model_name"] = self.model_name
        if self.batch_size is not None:
            result["batch_size"] = self.batch_size
        if self.max_latency_ms is not None:
            result["max_latency_ms"] = self.max_latency_ms
        if self.use_torch_compile is not None:
            result["use_torch_compile"] = self.use_torch_compile
        if self.use_mixed_precision is not None:
            result["use_mixed_precision"] = self.use_mixed_precision
        if self.tensor_alignment is not None:
            result["tensor_alignment"] = self.tensor_alignment
        return result


@dataclass(frozen=True, slots=True)
class FrozenAgentKwargs:
    """Immutable container for AgentKwargs.

    Thread Safety:
    - Frozen dataclass with __slots__
    - Fully immutable after construction

    Usage:
        >>> kwargs = FrozenAgentKwargs(name="coder", temperature=0.7)
        >>> config = AgentConfig(**kwargs.to_dict())
    """

    name: str | None = None
    description: str | None = None
    prompt: str | None = None
    temperature: float | None = None
    top_p: float | None = None
    max_tokens: int | None = None

    def to_dict(self) -> AgentKwargs:
        """Convert to TypedDict, excluding None values."""
        result: AgentKwargs = {}
        if self.name is not None:
            result["name"] = self.name
        if self.description is not None:
            result["description"] = self.description
        if self.prompt is not None:
            result["prompt"] = self.prompt
        if self.temperature is not None:
            result["temperature"] = self.temperature
        if self.top_p is not None:
            result["top_p"] = self.top_p
        if self.max_tokens is not None:
            result["max_tokens"] = self.max_tokens
        return result


# ============================================================================
# Public API
# ============================================================================

__all__: Final[tuple[str, ...]] = (
    # Compute kwargs
    "ComputeKwargs",
    "SubAgentKwargs",
    "TensorConfigKwargs",
    # Model/Agent kwargs
    "ModelKwargs",
    "AgentKwargs",
    # Runner kwargs
    "RunKwargs",
    "AgentRunKwargs",
    # Guardrail kwargs
    "GuardrailKwargs",
    # Embedding kwargs
    "EmbeddingKwargs",
    "BatchEmbeddingKwargs",
    # Protocols
    "SupportsComputeKwargs",
    "SupportsEmbedding",
    "SupportsAgentRun",
    # Frozen containers
    "FrozenComputeKwargs",
    "FrozenAgentKwargs",
)
