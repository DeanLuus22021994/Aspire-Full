"""Pytest configuration for python-agents tests.

Provides fixtures for:
- TensorCore GPU mocking
- SubAgent orchestrator setup
- Environment variable configuration

GPU-ONLY: Tests require CUDA GPU. Use ASPIRE_TEST_GPU=1 to run.
"""

from __future__ import annotations

import os
import sys
from collections.abc import Generator
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
SRC_ROOT = REPO_ROOT / "python-agents" / "src"

if str(SRC_ROOT) not in sys.path:
    sys.path.insert(0, str(SRC_ROOT))


def pytest_addoption(parser: pytest.Parser) -> None:  # pragma: no cover - pytest hook
    """Register ini options that pytest-asyncio would normally add."""

    parser.addini(
        "asyncio_mode",
        "Pytest-asyncio compatibility shim for local development",
        default="auto",
    )


@pytest.fixture(scope="session", autouse=True)
def setup_test_environment() -> Generator[None, None, None]:
    """Set up test environment - GPU required.

    GPU-ONLY: No CPU fallback. Tests require CUDA.
    """
    original_values = {
        "ASPIRE_COMPUTE_MODE": os.environ.get("ASPIRE_COMPUTE_MODE"),
    }

    # GPU-only mode
    os.environ["ASPIRE_COMPUTE_MODE"] = "gpu"

    yield

    # Restore original values
    for key, value in original_values.items():
        if value is None:
            os.environ.pop(key, None)
        else:
            os.environ[key] = value


@pytest.fixture
def subagent_config() -> "SubAgentConfig":
    """Create a test SubAgentConfig - GPU required."""
    from aspire_agents import SubAgentConfig

    return SubAgentConfig(
        max_concurrent=4,
        gpu_share_enabled=True,
        thread_pool_size=2,
        tensor_batch_size=8,
        compute_mode="gpu",
        tensor_alignment=128,
        offload_enabled=True,
    )


@pytest.fixture
def tensor_config() -> "TensorConfig":
    """Create a test TensorConfig - GPU required."""
    from aspire_agents import TensorConfig

    return TensorConfig(
        use_gpu=True,
        use_tensor_cores=True,
        use_flash_attention=True,
        batch_size=8,
        max_sequence_length=128,
        use_torch_compile=True,
        mixed_precision=True,
        tensor_alignment=128,
    )
