"""Thread-safe agent configuration for Python 3.15+ free-threaded runtime.

Provides immutable configuration classes using frozen dataclasses:
- ModelConfig: Model provider settings (OpenAI, Azure, GitHub)
- AgentConfig: Full agent configuration with prompt, model, and handoffs
- TensorConfig: GPU compute configuration for tensor operations

All configuration objects are immutable (frozen=True) and use __slots__
for memory efficiency. Thread-safe by design - no mutable state.

Python 3.15 Optimizations:
- Frozen dataclasses with __slots__ for zero-copy thread safety
- Immutable tuple collections instead of lists
- Type aliases for cleaner annotations

Environment Variables (from Dockerfile):
- ASPIRE_TENSOR_BATCH_SIZE: Default batch size for tensor ops (default: 32)
- CUDA_TENSOR_CORE_ALIGNMENT: Memory alignment in bytes (default: 128)

GPU-ONLY: This module requires a CUDA GPU. No CPU fallback is supported.
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Final, Literal, cast

import yaml

# Type alias for supported providers
ProviderLiteral = Literal["openai", "azure", "github", "anthropic", "local"]

# Default model for new agents - GPT-4.1 mini for cost efficiency
DEFAULT_MODEL: Final[str] = "gpt-4.1-mini"
DEFAULT_PROVIDER: Final[ProviderLiteral] = "openai"

# Environment variable defaults for tensor configuration - GPU-ONLY
_DEFAULT_BATCH_SIZE: Final[int] = int(os.environ.get("ASPIRE_TENSOR_BATCH_SIZE", "32"))
_TENSOR_ALIGNMENT: Final[int] = int(os.environ.get("CUDA_TENSOR_CORE_ALIGNMENT", "128"))


def _read_prompt(base: Path, prompt: str | None) -> str:
    """Load the prompt block referenced by an agent manifest.

    Args:
        base: Base directory for resolving relative paths
        prompt: Relative path to prompt file

    Returns:
        Prompt text content stripped of whitespace

    Raises:
        ValueError: If prompt is None or empty
        FileNotFoundError: If prompt file doesn't exist
    """
    if not prompt:
        raise ValueError("Agent config requires a 'prompt' field pointing to a text/markdown file.")

    prompt_path = (base / prompt).resolve()
    if not prompt_path.exists():
        raise FileNotFoundError(f"Prompt file not found: {prompt_path}")
    return prompt_path.read_text(encoding="utf-8").strip()


def _load_yaml(path: Path) -> dict[str, Any]:
    """Load and validate YAML configuration file.

    Args:
        path: Path to YAML file

    Returns:
        Parsed YAML as dictionary

    Raises:
        ValueError: If root is not a mapping
    """
    raw_data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(raw_data, dict):
        raise ValueError("Agent config root must be a mapping")
    return cast("dict[str, Any]", raw_data)


@dataclass(frozen=True, slots=True)
class TensorConfig:
    """Immutable tensor compute configuration.

    Controls GPU acceleration settings for embedding and inference.
    Thread-safe due to immutability. GPU is ALWAYS required.

    Default values are read from environment variables set in Dockerfiles:
    - ASPIRE_TENSOR_BATCH_SIZE for batch_size
    - CUDA_TENSOR_CORE_ALIGNMENT for memory alignment

    Attributes:
        use_gpu: Always True - GPU is required
        use_tensor_cores: Enable Tensor Core optimizations (FP16/TF32)
        use_flash_attention: Enable Flash Attention for transformers
        batch_size: Default batch size for batched operations
        max_sequence_length: Maximum token sequence length
        use_torch_compile: Enable torch.compile() optimization
        mixed_precision: Enable automatic mixed precision
        tensor_alignment: CUDA memory alignment in bytes (default: 128)
    """

    use_gpu: bool = True  # GPU is ALWAYS required
    use_tensor_cores: bool = True
    use_flash_attention: bool = True
    batch_size: int = field(default_factory=lambda: _DEFAULT_BATCH_SIZE)
    max_sequence_length: int = 512
    use_torch_compile: bool = True
    mixed_precision: bool = True
    tensor_alignment: int = field(default_factory=lambda: _TENSOR_ALIGNMENT)

    @classmethod
    def from_env(cls) -> TensorConfig:
        """Create TensorConfig from environment variables.

        Reads ASPIRE_TENSOR_BATCH_SIZE and CUDA_TENSOR_CORE_ALIGNMENT.
        GPU is always required.

        Returns:
            TensorConfig with environment-based defaults
        """
        return cls()


@dataclass(frozen=True, slots=True)
class ModelConfig:
    """Immutable model provider configuration.

    Supports OpenAI, Azure OpenAI, GitHub-hosted, and local models.
    Thread-safe due to immutability (frozen=True) and __slots__.

    Attributes:
        provider: Model provider (openai, azure, github, anthropic, local)
        name: Model name or deployment name
        deployment: Azure deployment name (optional)
        endpoint: Custom API endpoint (optional)
        api_version: API version for Azure (optional)

    Examples:
        >>> ModelConfig()  # Default GPT-4.1-mini
        >>> ModelConfig(name="gpt-4o")
        >>> ModelConfig(provider="azure", deployment="my-gpt4")
        >>> ModelConfig.from_string("gpt-4o-mini")
    """

    provider: ProviderLiteral = DEFAULT_PROVIDER
    name: str = DEFAULT_MODEL
    deployment: str | None = None
    endpoint: str | None = None
    api_version: str | None = None

    @classmethod
    def from_string(cls, model_name: str) -> ModelConfig:
        """Create ModelConfig from a simple model name string.

        Args:
            model_name: Model name (e.g., "gpt-4o-mini")

        Returns:
            ModelConfig with default provider and specified name
        """
        return cls(name=model_name)

    @classmethod
    def from_mapping(cls, data: Any) -> ModelConfig:
        """Construct from YAML scalar or mapping.

        Supports both string shorthand and full mapping:
        - "gpt-4o" -> ModelConfig(name="gpt-4o")
        - {name: "gpt-4o", provider: "azure"} -> full config

        Args:
            data: String model name or dict configuration

        Returns:
            Configured ModelConfig instance

        Raises:
            ValueError: If data is neither string nor dict
        """
        if isinstance(data, str):
            return cls.from_string(data)

        if not isinstance(data, dict):
            raise ValueError("model section must be a string or mapping")

        # Cast dict values to proper types
        data_dict = cast("dict[str, Any]", data)
        return cls(
            provider=str(data_dict.get("provider", DEFAULT_PROVIDER)),  # type: ignore[arg-type]
            name=str(data_dict.get("name", data_dict.get("deployment", DEFAULT_MODEL))),
            deployment=str(data_dict.get("deployment")) if data_dict.get("deployment") else None,
            endpoint=str(data_dict.get("endpoint")) if data_dict.get("endpoint") else None,
            api_version=str(data_dict.get("api_version")) if data_dict.get("api_version") else None,
        )

    def with_name(self, name: str) -> ModelConfig:
        """Create a copy with a different model name."""
        return ModelConfig(
            provider=self.provider,
            name=name,
            deployment=self.deployment,
            endpoint=self.endpoint,
            api_version=self.api_version,
        )


@dataclass(frozen=True, slots=True)
class AgentConfig:
    """Immutable top-level agent configuration.

    Loaded from YAML manifests with prompt file references.
    Thread-safe due to immutability (frozen=True) and __slots__.

    Attributes:
        name: Agent identifier
        description: Human-readable description
        prompt: System prompt/instructions
        model: Model configuration
        tensor: Tensor compute configuration
        temperature: Sampling temperature (0.0 = deterministic)
        top_p: Nucleus sampling parameter (optional)
        handoffs: Tuple of agent names for handoff capability
        tags: Tuple of tags for categorization
        max_tokens: Maximum output tokens (optional)

    Examples:
        >>> AgentConfig.from_file(Path("agents/coder/agent.yaml"))
        >>> AgentConfig(name="test", prompt="Help me", model=ModelConfig())
    """

    name: str = "default-agent"
    description: str = ""
    prompt: str = "You are a helpful AI assistant."
    model: ModelConfig = field(default_factory=ModelConfig)
    tensor: TensorConfig = field(default_factory=TensorConfig)
    temperature: float = 0.0
    top_p: float | None = None
    handoffs: tuple[str, ...] = field(default_factory=tuple)
    tags: tuple[str, ...] = field(default_factory=tuple)
    max_tokens: int | None = None

    # Alias for backwards compatibility
    @property
    def instructions(self) -> str:
        """Alias for prompt (backwards compatibility)."""
        return self.prompt

    @classmethod
    def from_file(cls, path: Path) -> AgentConfig:
        """Parse an agent manifest from disk.

        Args:
            path: Path to agent YAML manifest

        Returns:
            Fully configured AgentConfig

        Raises:
            FileNotFoundError: If manifest or prompt file not found
            ValueError: If manifest is invalid
        """
        payload = _load_yaml(path)
        base = path.parent
        prompt_body = _read_prompt(base, payload.get("prompt"))
        model = ModelConfig.from_mapping(payload.get("model", {}))

        # Parse tensor config if present
        tensor_data = payload.get("tensor", {})
        tensor = TensorConfig(
            use_gpu=tensor_data.get("use_gpu", True),
            use_tensor_cores=tensor_data.get("use_tensor_cores", True),
            use_flash_attention=tensor_data.get("use_flash_attention", True),
            batch_size=tensor_data.get("batch_size", 32),
            max_sequence_length=tensor_data.get("max_sequence_length", 512),
            use_torch_compile=tensor_data.get("use_torch_compile", True),
            mixed_precision=tensor_data.get("mixed_precision", True),
        )

        return cls(
            name=payload.get("name", base.name),
            description=payload.get("description", ""),
            prompt=prompt_body,
            model=model,
            tensor=tensor,
            temperature=float(payload.get("temperature", 0.0)),
            top_p=(float(payload["top_p"]) if payload.get("top_p") is not None else None),
            handoffs=tuple(payload.get("handoffs", [])),
            tags=tuple(payload.get("tags", [])),
            max_tokens=payload.get("max_tokens"),
        )

    def as_prompt(self, user_input: str) -> str:
        """Combine the instructions with user input.

        Args:
            user_input: User's request text

        Returns:
            Full prompt with system instructions and user input
        """
        segments = [self.prompt]
        newline = "" if self.prompt.endswith("\n") else "\n"
        segments.append(newline)
        segments.append("User request:\n")
        segments.append(user_input.strip())
        return "\n".join(segments)

    def with_model(self, model: ModelConfig | str) -> AgentConfig:
        """Create a copy with a different model configuration.

        Args:
            model: New ModelConfig or model name string

        Returns:
            New AgentConfig with updated model
        """
        if isinstance(model, str):
            model = ModelConfig.from_string(model)
        return AgentConfig(
            name=self.name,
            description=self.description,
            prompt=self.prompt,
            model=model,
            tensor=self.tensor,
            temperature=self.temperature,
            top_p=self.top_p,
            handoffs=self.handoffs,
            tags=self.tags,
            max_tokens=self.max_tokens,
        )

    def with_tensor(self, tensor: TensorConfig) -> AgentConfig:
        """Create a copy with different tensor configuration.

        Args:
            tensor: New TensorConfig

        Returns:
            New AgentConfig with updated tensor config
        """
        return AgentConfig(
            name=self.name,
            description=self.description,
            prompt=self.prompt,
            model=self.model,
            tensor=tensor,
            temperature=self.temperature,
            top_p=self.top_p,
            handoffs=self.handoffs,
            tags=self.tags,
            max_tokens=self.max_tokens,
        )
