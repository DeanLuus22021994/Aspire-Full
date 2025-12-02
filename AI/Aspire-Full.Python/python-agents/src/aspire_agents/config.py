"""Thread-safe agent configuration for Python 3.15+ free-threaded runtime.

Provides immutable configuration classes using frozen dataclasses:
- ModelConfig: Model provider settings (OpenAI, Azure, GitHub)
- AgentConfig: Full agent configuration with prompt, model, and handoffs

All configuration objects are immutable (frozen=True) and use __slots__
for memory efficiency. Thread-safe by design - no mutable state.

Python 3.15 Optimizations:
- PEP 695 style type aliases (when stable)
- Frozen dataclasses with __slots__ for zero-copy thread safety
- Immutable tuple collections instead of lists
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Final, Literal, cast, overload

import yaml

# Type alias for supported providers
ProviderLiteral = Literal["openai", "azure", "github"]

# Default model for new agents - GPT-4.1 mini for cost efficiency
DEFAULT_MODEL: Final[str] = "gpt-4.1-mini"
DEFAULT_PROVIDER: Final[ProviderLiteral] = "openai"


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
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError("Agent config root must be a mapping")
    return cast(dict[str, Any], data)


@dataclass(frozen=True, slots=True)
class ModelConfig:
    """Immutable model provider configuration.

    Supports OpenAI, Azure OpenAI, and GitHub-hosted models.
    Thread-safe due to immutability (frozen=True) and __slots__.

    Attributes:
        provider: Model provider (openai, azure, github)
        name: Model name or deployment name
        deployment: Azure deployment name (optional)
        endpoint: Custom API endpoint (optional)

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

        data_dict = cast(dict[str, Any], data)
        return cls(
            provider=cast(ProviderLiteral, data_dict.get("provider", DEFAULT_PROVIDER)),
            name=str(data_dict.get("name", data_dict.get("deployment", DEFAULT_MODEL))),
            deployment=cast(str | None, data_dict.get("deployment")),
            endpoint=cast(str | None, data_dict.get("endpoint")),
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
        temperature: Sampling temperature (0.0 = deterministic)
        top_p: Nucleus sampling parameter (optional)
        handoffs: Tuple of agent names for handoff capability
        tags: Tuple of tags for categorization

    Examples:
        >>> AgentConfig.from_file(Path("agents/coder/agent.yaml"))
        >>> AgentConfig(name="test", description="", prompt="Help me", model=ModelConfig())
    """

    name: str = "default-agent"
    description: str = ""
    prompt: str = "You are a helpful AI assistant."
    model: ModelConfig = field(default_factory=ModelConfig)
    temperature: float = 0.0
    top_p: float | None = None
    handoffs: tuple[str, ...] = field(default_factory=tuple)
    tags: tuple[str, ...] = field(default_factory=tuple)

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
        prompt_body = _read_prompt(base, cast(str | None, payload.get("prompt")))
        model = ModelConfig.from_mapping(payload.get("model", {}))

        return cls(
            name=cast(str, payload.get("name", base.name)),
            description=cast(str, payload.get("description", "")),
            prompt=prompt_body,
            model=model,
            temperature=float(cast(float | str, payload.get("temperature", 0.0))),
            top_p=(float(cast(float | str, payload["top_p"])) if payload.get("top_p") is not None else None),
            handoffs=tuple(payload.get("handoffs", [])),
            tags=tuple(payload.get("tags", [])),
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
        # Use object.__new__ to bypass frozen restriction for copy
        return AgentConfig(
            name=self.name,
            description=self.description,
            prompt=self.prompt,
            model=model,
            temperature=self.temperature,
            top_p=self.top_p,
            handoffs=self.handoffs,
            tags=self.tags,
        )
