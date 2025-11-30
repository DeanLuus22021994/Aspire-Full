"""Tests for aspire_agents.config helpers."""

from __future__ import annotations

import sys
from pathlib import Path
from textwrap import dedent

# Add src to path so we can import aspire_agents
sys.path.append(str(Path(__file__).parents[1] / "src"))

import pytest  # type: ignore # noqa: E402 # pylint: disable=wrong-import-position

from aspire_agents.config import (  # noqa: E402 # pylint: disable=wrong-import-position, import-error
    AgentConfig,
    ModelConfig,
)


def test_model_config_from_string_defaults() -> None:
    cfg = ModelConfig.from_mapping("gpt-4o-mini")

    assert cfg.name == "gpt-4o-mini"
    assert cfg.provider == "openai"
    assert cfg.deployment is None
    assert cfg.endpoint is None


def test_model_config_from_mapping_overrides() -> None:
    cfg = ModelConfig.from_mapping(
        {
            "provider": "azure",
            "name": "gpt-4o",
            "deployment": "gpt-4o-dev",
            "endpoint": "https://example.openai.azure.com",
        }
    )

    assert cfg.provider == "azure"
    assert cfg.name == "gpt-4o"
    assert cfg.deployment == "gpt-4o-dev"
    assert cfg.endpoint == "https://example.openai.azure.com"


def test_agent_config_from_file_loads_prompt(tmp_path: Path) -> None:
    prompt_dir = tmp_path / "prompts"
    prompt_dir.mkdir()
    prompt_path = prompt_dir / "instructions.md"
    prompt_path.write_text("Keep calm", encoding="utf-8")

    manifest = tmp_path / "agent.yaml"
    payload = dedent(
        """
        name: helper
        description: does things
        prompt: prompts/instructions.md
        model:
          provider: openai
          name: gpt-4o-mini
        temperature: 0.3
        top_p: 0.9
        handoffs:
          - escalate
        tags:
          - sandbox
        """
    ).strip()
    manifest.write_text(payload, encoding="utf-8")

    cfg = AgentConfig.from_file(manifest)

    assert cfg.name == "helper"
    assert cfg.description == "does things"
    assert cfg.prompt == "Keep calm"
    assert cfg.model.name == "gpt-4o-mini"
    assert pytest.approx(cfg.temperature, rel=1e-6) == 0.3
    assert pytest.approx(cfg.top_p, rel=1e-6) == 0.9
    assert cfg.handoffs == ["escalate"]
    assert cfg.tags == ["sandbox"]


def test_agent_config_requires_prompt_key(tmp_path: Path) -> None:
    manifest = tmp_path / "agent.yaml"
    manifest.write_text("name: helper", encoding="utf-8")

    with pytest.raises(ValueError):
        AgentConfig.from_file(manifest)


def test_as_prompt_injects_user_input() -> None:
    cfg = AgentConfig(
        name="helper",
        description="",
        prompt="Act nice.",
        model=ModelConfig(name="gpt-4o"),
        temperature=0.0,
    )

    result = cfg.as_prompt("Tell me a joke")

    assert "Act nice." in result
    assert "User request:" in result
    assert result.rstrip().endswith("Tell me a joke")
