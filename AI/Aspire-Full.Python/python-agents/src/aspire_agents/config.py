"""Thread-safe agent configuration for Python 3.15+ free-threaded runtime.

Provides immutable configuration classes using frozen dataclasses:
- ModelConfig: Model provider settings (OpenAI, Azure)
- AgentConfig: Full agent configuration with prompt, model, and handoffs

All configuration objects are immutable (frozen=True) and use __slots__
for memory efficiency. Thread-safe by design - no mutable state.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Final, Literal, cast

import yaml

ProviderLiteral = Literal["openai", "azure", "github"]

# Default model for new agents
DEFAULT_MODEL: Final[str] = "gpt-4.1-mini"


def _read_prompt(base: Path, prompt: str | None) -> str:
    """Load the prompt block referenced by an agent manifest."""

    if not prompt:
        raise ValueError("Agent config requires a 'prompt' field pointing to a " "text/markdown file.")

    prompt_path = (base / prompt).resolve()
    if not prompt_path.exists():
        raise FileNotFoundError(f"Prompt file not found: {prompt_path}")
    return prompt_path.read_text(encoding="utf-8").strip()


def _load_yaml(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError("Agent config root must be a mapping")
    return cast(dict[str, Any], data)


@dataclass(frozen=True, slots=True)
class ModelConfig:
    """Immutable model provider configuration.

    Supports OpenAI, Azure OpenAI, and GitHub-hosted models.
    Thread-safe due to immutability (frozen=True).
    """

    provider: ProviderLiteral = "openai"
    name: str = DEFAULT_MODEL
    deployment: str | None = None
    endpoint: str | None = None

    @classmethod
    def from_mapping(cls, data: Any) -> ModelConfig:
        """Construct from YAML scalar or mapping."""
        if isinstance(data, str):
            return cls(name=data)

        if not isinstance(data, dict):  # pragma: no cover - defensive
            raise ValueError("model section must be a string or mapping")

        data_dict = cast(dict[str, Any], data)
        return cls(
            provider=cast(ProviderLiteral, data_dict.get("provider", "openai")),
            name=str(data_dict.get("name", data_dict.get("deployment", DEFAULT_MODEL))),
            deployment=cast(str | None, data_dict.get("deployment")),
            endpoint=cast(str | None, data_dict.get("endpoint")),
        )


@dataclass(frozen=True, slots=True)
class AgentConfig:
    """Immutable top-level agent configuration.

    Loaded from YAML manifests with prompt file references.
    Thread-safe due to immutability (frozen=True).
    """

    name: str
    description: str
    prompt: str
    model: ModelConfig
    temperature: float = 0.0
    top_p: float | None = None
    handoffs: tuple[str, ...] = field(default_factory=tuple)  # Immutable tuple
    tags: tuple[str, ...] = field(default_factory=tuple)  # Immutable tuple

    @classmethod
    def from_file(cls, path: Path) -> AgentConfig:
        """Parse an agent manifest from disk."""
        payload = _load_yaml(path)
        base = path.parent
        prompt_body = _read_prompt(base, cast(str | None, payload.get("prompt")))
        model = ModelConfig.from_mapping(payload.get("model"))

        return cls(
            name=cast(str, payload.get("name", base.name)),
            description=cast(str, payload.get("description", "")),
            prompt=prompt_body,
            model=model,
            temperature=float(cast(float | str, payload.get("temperature", 0.2))),
            top_p=(float(cast(float | str, payload["top_p"])) if payload.get("top_p") is not None else None),
            handoffs=tuple(payload.get("handoffs", [])),  # Convert to tuple
            tags=tuple(payload.get("tags", [])),  # Convert to tuple
        )

    def as_prompt(self, user_input: str) -> str:
        """Combine the instructions with user input."""
        segments = [self.prompt]
        newline = "" if self.prompt.endswith("\n") else "\n"
        segments.append(newline)
        segments.append("User request:\n")
        segments.append(user_input.strip())
        return "\n".join(segments)
