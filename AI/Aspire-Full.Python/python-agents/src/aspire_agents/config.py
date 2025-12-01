"""Agent configuration loading helpers."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Literal

import yaml

ProviderLiteral = Literal["openai", "azure"]


def _read_prompt(base: Path, prompt: str | None) -> str:
    """Load the prompt block referenced by an agent manifest."""

    if not prompt:
        raise ValueError(
            "Agent config requires a 'prompt' field pointing to a "
            "text/markdown file."
        )

    prompt_path = (base / prompt).resolve()
    if not prompt_path.exists():
        raise FileNotFoundError(f"Prompt file not found: {prompt_path}")
    return prompt_path.read_text(encoding="utf-8").strip()


def _load_yaml(path: Path) -> dict[str, Any]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError("Agent config root must be a mapping")
    return data


@dataclass(slots=True)
class ModelConfig:
    """Model provider description for a single agent."""

    provider: ProviderLiteral = "openai"
    name: str = "gpt-4.1-mini"
    deployment: str | None = None
    endpoint: str | None = None

    @classmethod
    def from_mapping(cls, data: Any) -> "ModelConfig":
        """Construct a model config from a YAML scalar or mapping."""
        if isinstance(data, str):
            return cls(name=data)

        if not isinstance(data, dict):  # pragma: no cover - defensive
            raise ValueError("model section must be a string or mapping")

        return cls(
            provider=data.get("provider", "openai"),
            name=str(data.get("name", data.get("deployment", "gpt-4.1-mini"))),
            deployment=data.get("deployment"),
            endpoint=data.get("endpoint"),
        )


@dataclass(slots=True)
class AgentConfig:
    """Top-level configuration for a runnable agent."""

    name: str
    description: str
    prompt: str
    model: ModelConfig
    temperature: float = 0.0
    top_p: float | None = None
    handoffs: list[str] = field(default_factory=list)
    tags: list[str] = field(default_factory=list)

    @classmethod
    def from_file(cls, path: Path) -> "AgentConfig":
        """Parse an agent manifest from disk."""
        payload = _load_yaml(path)
        base = path.parent
        prompt_body = _read_prompt(base, payload.get("prompt"))
        model = ModelConfig.from_mapping(payload.get("model"))

        return cls(
            name=payload.get("name", base.name),
            description=payload.get("description", ""),
            prompt=prompt_body,
            model=model,
            temperature=float(payload.get("temperature", 0.2)),
            top_p=(
                float(payload["top_p"]) if payload.get("top_p") is not None else None
            ),
            handoffs=list(payload.get("handoffs", [])),
            tags=list(payload.get("tags", [])),
        )

    def as_prompt(self, user_input: str) -> str:
        """Combine the instructions with user input."""
        segments = [self.prompt]
        newline = "" if self.prompt.endswith("\n") else "\n"
        segments.append(newline)
        segments.append("User request:\n")
        segments.append(user_input.strip())
        return "\n".join(segments)
