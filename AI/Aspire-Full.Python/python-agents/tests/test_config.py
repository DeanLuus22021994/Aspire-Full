"""Tests for aspire_agents.config helpers.

Thread-safe tests for configuration classes including:
- ModelConfig: Provider settings (immutable)
- AgentConfig: Full agent configuration (immutable)
- TensorConfig: GPU compute settings with environment variable defaults

All configuration classes are frozen dataclasses, ensuring thread safety.
Tests verify immutability and correct defaults.
"""

from __future__ import annotations

import sys
import threading
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from textwrap import dedent

import pytest

# Add src to path for imports
_SRC_PATH = str(Path(__file__).parents[1] / "src")
if _SRC_PATH not in sys.path:
    sys.path.insert(0, _SRC_PATH)

from aspire_agents.config import (
    AgentConfig,
    ModelConfig,
    TensorConfig,
)

# ============================================================================
# ModelConfig Tests
# ============================================================================


class TestModelConfig:
    """Tests for ModelConfig frozen dataclass."""

    def test_from_string_defaults(self) -> None:
        """Test ModelConfig creation from string uses correct defaults."""
        cfg = ModelConfig.from_mapping("gpt-4o-mini")

        assert cfg.name == "gpt-4o-mini"
        assert cfg.provider == "openai"
        assert cfg.deployment is None
        assert cfg.endpoint is None

    def test_from_mapping_overrides(self) -> None:
        """Test ModelConfig creation from mapping with all fields."""
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

    def test_immutable(self) -> None:
        """Test that ModelConfig is truly frozen (immutable)."""
        cfg = ModelConfig()

        with pytest.raises(AttributeError):
            cfg.name = "modified"  # type: ignore[misc]

    def test_with_name_creates_copy(self) -> None:
        """Test with_name() creates new instance, original unchanged."""
        original = ModelConfig(name="gpt-4o")
        modified = original.with_name("gpt-4o-mini")

        assert modified.name == "gpt-4o-mini"
        assert original.name == "gpt-4o"  # Original unchanged

    @pytest.mark.thread_safe
    def test_concurrent_creation(self) -> None:
        """Test thread-safe concurrent ModelConfig creation."""
        results: list[ModelConfig] = []
        errors: list[Exception] = []

        def create_config(name: str) -> None:
            try:
                cfg = ModelConfig.from_mapping({"name": name, "provider": "openai"})
                results.append(cfg)
            except Exception as e:
                errors.append(e)

        with ThreadPoolExecutor(max_workers=8) as executor:
            futures = [executor.submit(create_config, f"model-{i}") for i in range(100)]
            for f in futures:
                f.result()

        assert len(errors) == 0, f"Errors during concurrent creation: {errors}"
        assert len(results) == 100


# ============================================================================
# TensorConfig Tests
# ============================================================================


class TestTensorConfig:
    """Tests for TensorConfig frozen dataclass."""

    def test_defaults(self) -> None:
        """Test TensorConfig default values from environment."""
        cfg = TensorConfig()

        assert isinstance(cfg.batch_size, int)
        assert isinstance(cfg.tensor_alignment, int)
        assert cfg.tensor_alignment == 128  # CUDA_TENSOR_CORE_ALIGNMENT default
        assert cfg.use_tensor_cores is True
        assert cfg.use_flash_attention is True
        assert cfg.use_torch_compile is True
        assert cfg.mixed_precision is True
        assert cfg.use_gpu is True  # GPU is always required

    def test_from_env(self) -> None:
        """Test TensorConfig.from_env() factory method."""
        cfg = TensorConfig.from_env()

        assert cfg.batch_size > 0
        assert cfg.tensor_alignment > 0
        assert cfg.use_gpu is True

    def test_immutable(self) -> None:
        """Test that TensorConfig is truly frozen."""
        cfg = TensorConfig()

        with pytest.raises(AttributeError):
            cfg.batch_size = 64  # type: ignore[misc]

    def test_custom_values(self) -> None:
        """Test TensorConfig with custom values."""
        cfg = TensorConfig(
            batch_size=64,
            max_sequence_length=256,
            use_torch_compile=False,
        )

        assert cfg.batch_size == 64
        assert cfg.max_sequence_length == 256
        assert cfg.use_torch_compile is False

    @pytest.mark.thread_safe
    def test_concurrent_access(self) -> None:
        """Test thread-safe concurrent TensorConfig access."""
        cfg = TensorConfig(batch_size=32)
        results: list[int] = []

        def read_batch_size() -> None:
            for _ in range(1000):
                results.append(cfg.batch_size)

        threads = [threading.Thread(target=read_batch_size) for _ in range(4)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()

        # All reads should return consistent value
        assert all(x == 32 for x in results)


# ============================================================================
# AgentConfig Tests
# ============================================================================


class TestAgentConfig:
    """Tests for AgentConfig frozen dataclass."""

    def test_from_file_loads_prompt(self, tmp_path: Path) -> None:
        """Test AgentConfig.from_file loads prompt from file."""
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
        assert abs(cfg.temperature - 0.3) < 1e-6
        assert cfg.top_p is not None
        assert abs(cfg.top_p - 0.9) < 1e-6
        assert cfg.handoffs == ("escalate",)
        assert cfg.tags == ("sandbox",)

    def test_requires_prompt_key(self, tmp_path: Path) -> None:
        """Test AgentConfig.from_file raises on missing prompt."""
        manifest = tmp_path / "agent.yaml"
        manifest.write_text("name: helper", encoding="utf-8")

        with pytest.raises(ValueError):
            AgentConfig.from_file(manifest)

    def test_as_prompt_injects_user_input(self) -> None:
        """Test as_prompt() combines instructions with user input."""
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

    def test_with_tensor_creates_copy(self) -> None:
        """Test AgentConfig.with_tensor() creates new config."""
        original = AgentConfig(
            name="test",
            prompt="Test prompt",
            model=ModelConfig(name="gpt-4o"),
        )

        new_tensor = TensorConfig(batch_size=64)
        modified = original.with_tensor(new_tensor)

        assert modified.tensor.batch_size == 64
        assert original.tensor.batch_size != 64  # Original unchanged

    def test_with_model_string(self) -> None:
        """Test AgentConfig.with_model() with string argument."""
        original = AgentConfig(
            name="test",
            prompt="Test",
            model=ModelConfig(name="gpt-4o"),
        )

        modified = original.with_model("gpt-4o-mini")

        assert modified.model.name == "gpt-4o-mini"
        assert original.model.name == "gpt-4o"

    def test_instructions_alias(self) -> None:
        """Test instructions property is alias for prompt."""
        cfg = AgentConfig(name="test", prompt="My instructions")

        assert cfg.instructions == cfg.prompt
        assert cfg.instructions == "My instructions"

    @pytest.mark.thread_safe
    def test_concurrent_from_file(self, tmp_path: Path) -> None:
        """Test thread-safe concurrent file loading."""
        # Create test files
        for i in range(10):
            prompt_path = tmp_path / f"prompt_{i}.md"
            prompt_path.write_text(f"Prompt {i}", encoding="utf-8")

            manifest = tmp_path / f"agent_{i}.yaml"
            manifest.write_text(
                f"name: agent-{i}\nprompt: prompt_{i}.md\n",
                encoding="utf-8",
            )

        results: list[AgentConfig] = []
        errors: list[Exception] = []
        lock = threading.Lock()

        def load_config(idx: int) -> None:
            try:
                cfg = AgentConfig.from_file(tmp_path / f"agent_{idx}.yaml")
                with lock:
                    results.append(cfg)
            except Exception as e:
                with lock:
                    errors.append(e)

        with ThreadPoolExecutor(max_workers=4) as executor:
            futures = [executor.submit(load_config, i) for i in range(10)]
            for f in futures:
                f.result()

        assert len(errors) == 0, f"Errors during concurrent load: {errors}"
        assert len(results) == 10


# ============================================================================
# Integration Tests
# ============================================================================


class TestConfigIntegration:
    """Integration tests for configuration classes."""

    def test_full_config_chain(self, tmp_path: Path) -> None:
        """Test creating full configuration chain."""
        # Create prompt file
        prompt_path = tmp_path / "system.md"
        prompt_path.write_text("You are a helpful assistant.", encoding="utf-8")

        # Create manifest
        manifest = tmp_path / "agent.yaml"
        manifest.write_text(
            dedent(
                """
                name: assistant
                description: A helpful AI
                prompt: system.md
                model:
                  provider: azure
                  name: gpt-4o
                  deployment: gpt-4o-prod
                tensor:
                  use_gpu: true
                  batch_size: 16
                temperature: 0.7
                """
            ).strip(),
            encoding="utf-8",
        )

        cfg = AgentConfig.from_file(manifest)

        # Verify chain
        assert cfg.name == "assistant"
        assert cfg.model.provider == "azure"
        assert cfg.model.deployment == "gpt-4o-prod"
        assert cfg.tensor.use_gpu is True
        assert cfg.tensor.batch_size == 16

    def test_config_hashable(self) -> None:
        """Test that frozen configs can be used as dict keys."""
        cfg1 = ModelConfig(name="gpt-4o")
        cfg2 = ModelConfig(name="gpt-4o-mini")

        config_map: dict[ModelConfig, str] = {
            cfg1: "first",
            cfg2: "second",
        }

        assert config_map[cfg1] == "first"
        assert config_map[cfg2] == "second"

    def test_config_equality(self) -> None:
        """Test that identical configs are equal."""
        cfg1 = TensorConfig(batch_size=32, tensor_alignment=128)
        cfg2 = TensorConfig(batch_size=32, tensor_alignment=128)

        assert cfg1 == cfg2

    def test_config_inequality(self) -> None:
        """Test that different configs are not equal."""
        cfg1 = TensorConfig(batch_size=32)
        cfg2 = TensorConfig(batch_size=64)

        assert cfg1 != cfg2
