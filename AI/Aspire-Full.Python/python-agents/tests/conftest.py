"""Pytest configuration for python-agents tests.

Provides fixtures for:
- TensorCore GPU mocking
- SubAgent orchestrator setup
- Environment variable configuration

Environment Variables:
- ASPIRE_COMPUTE_MODE: Set to 'cpu' for test isolation
- ASPIRE_ALLOW_CPU_FALLBACK: Set to '1' for CI environments
"""

from __future__ import annotations

import os
import sys
from pathlib import Path
from typing import Generator

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
SRC_ROOT = REPO_ROOT / "python-agents" / "src"

if str(SRC_ROOT) not in sys.path:
    sys.path.insert(0, str(SRC_ROOT))


def pytest_addoption(parser):  # pragma: no cover - pytest hook
    """Register ini options that pytest-asyncio would normally add."""

    parser.addini(
        "asyncio_mode",
        "Pytest-asyncio compatibility shim for local development",
        default="auto",
    )


@pytest.fixture(scope="session", autouse=True)
def setup_test_environment() -> Generator[None, None, None]:
    """Set up test environment with CPU fallback enabled.

    This ensures tests can run in CI without GPU.
    """
    original_values = {
        "ASPIRE_COMPUTE_MODE": os.environ.get("ASPIRE_COMPUTE_MODE"),
        "ASPIRE_ALLOW_CPU_FALLBACK": os.environ.get("ASPIRE_ALLOW_CPU_FALLBACK"),
    }

    # Force CPU mode for tests unless GPU is explicitly available
    if not os.environ.get("ASPIRE_TEST_GPU", ""):
        os.environ["ASPIRE_COMPUTE_MODE"] = "cpu"
        os.environ["ASPIRE_ALLOW_CPU_FALLBACK"] = "1"

    yield

    # Restore original values
    for key, value in original_values.items():
        if value is None:
            os.environ.pop(key, None)
        else:
            os.environ[key] = value


@pytest.fixture
def subagent_config():
    """Create a test SubAgentConfig with CPU mode."""
    from aspire_agents import SubAgentConfig

    return SubAgentConfig(
        max_concurrent=4,
        gpu_share_enabled=False,
        thread_pool_size=2,
        tensor_batch_size=8,
        compute_mode="cpu",
        tensor_alignment=128,
        offload_enabled=False,
    )


@pytest.fixture
def tensor_config():
    """Create a test TensorConfig with CPU mode."""
    from aspire_agents import TensorConfig

    return TensorConfig(
        use_gpu=False,
        use_tensor_cores=False,
        use_flash_attention=False,
        batch_size=8,
        max_sequence_length=128,
        use_torch_compile=False,
        mixed_precision=False,
        tensor_alignment=128,
    )
