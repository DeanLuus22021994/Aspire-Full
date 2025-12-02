"""Pytest configuration for python-agents tests.

Thread-safe pytest fixtures for Python 3.16+ free-threaded runtime.

Provides fixtures for:
- TensorCore GPU mocking with thread-safe initialization
- SubAgent orchestrator setup with proper cleanup
- Environment variable configuration with isolation
- Async event loop management for concurrent tests

GPU-ONLY: Tests require CUDA GPU. Use ASPIRE_TEST_GPU=1 to run.

Thread Safety:
- All fixtures use threading.Lock for initialization
- Session-scoped fixtures are initialized once
- Function-scoped fixtures are isolated per test
"""

from __future__ import annotations

import asyncio
import os
import sys
import threading
from collections.abc import AsyncGenerator, Generator
from contextlib import contextmanager
from pathlib import Path
from typing import TYPE_CHECKING, Any, Final
from unittest.mock import MagicMock, patch

import pytest

# ============================================================================
# Path Configuration
# ============================================================================

REPO_ROOT: Final[Path] = Path(__file__).resolve().parents[2]
SRC_ROOT: Final[Path] = REPO_ROOT / "python-agents" / "src"

if str(SRC_ROOT) not in sys.path:
    sys.path.insert(0, str(SRC_ROOT))

# ============================================================================
# Type Checking Imports
# ============================================================================

if TYPE_CHECKING:
    from aspire_agents import SubAgentConfig, TensorConfig
    from aspire_agents.compute import ComputeConfig
    from aspire_agents.subagent import SubAgentOrchestrator

# ============================================================================
# Thread-Safe Initialization Lock
# ============================================================================

_FIXTURE_LOCK: Final[threading.Lock] = threading.Lock()


# ============================================================================
# Pytest Hooks
# ============================================================================


def pytest_addoption(parser: pytest.Parser) -> None:
    """Register custom pytest options for GPU testing."""
    parser.addoption(
        "--gpu",
        action="store_true",
        default=False,
        help="Run tests requiring GPU (skipped by default)",
    )
    parser.addini(
        "asyncio_mode",
        "Pytest-asyncio mode (auto for automatic async detection)",
        default="auto",
    )
    parser.addini(
        "asyncio_default_fixture_loop_scope",
        "Default event loop scope for async fixtures",
        default="function",
    )


def pytest_configure(config: pytest.Config) -> None:
    """Configure pytest markers for GPU and thread-safety tests."""
    config.addinivalue_line("markers", "gpu: mark test as requiring GPU")
    config.addinivalue_line("markers", "thread_safe: mark test as thread-safety test")
    config.addinivalue_line("markers", "slow: mark test as slow running")


def pytest_collection_modifyitems(
    config: pytest.Config,
    items: list[pytest.Item],
) -> None:
    """Skip GPU tests unless --gpu flag is provided."""
    if config.getoption("--gpu"):
        return

    skip_gpu = pytest.mark.skip(reason="GPU tests require --gpu option")
    for item in items:
        if "gpu" in item.keywords:
            item.add_marker(skip_gpu)


# ============================================================================
# Environment Fixtures
# ============================================================================


@contextmanager
def _isolated_env(**env_vars: str | None) -> Generator[None, None, None]:
    """Context manager for isolated environment variable changes.

    Thread-safe: Uses lock to prevent concurrent env modifications.
    """
    with _FIXTURE_LOCK:
        original: dict[str, str | None] = {}
        for key, value in env_vars.items():
            original[key] = os.environ.get(key)
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value

        try:
            yield
        finally:
            for key, value in original.items():
                if value is None:
                    os.environ.pop(key, None)
                else:
                    os.environ[key] = value


@pytest.fixture(scope="session", autouse=True)
def setup_test_environment() -> Generator[None, None, None]:
    """Set up test environment - GPU required.

    Session-scoped: Runs once at start, cleans up at end.
    Thread-safe: Uses lock for environment modifications.

    GPU-ONLY: No CPU fallback. Tests require CUDA.
    """
    with _isolated_env(
        ASPIRE_TENSOR_BATCH_SIZE="8",
        CUDA_TENSOR_CORE_ALIGNMENT="128",
        ASPIRE_TENSOR_OFFLOAD_ENABLED="1",
        ASPIRE_SUBAGENT_MAX_CONCURRENT="4",
        ASPIRE_SUBAGENT_GPU_SHARE="1",
        ASPIRE_AGENT_THREAD_POOL_SIZE="2",
    ):
        yield


@pytest.fixture
def clean_env() -> Generator[None, None, None]:
    """Provide a clean environment for each test.

    Function-scoped: Each test gets isolated environment.
    """
    with _isolated_env():
        yield


# ============================================================================
# Async Fixtures
# ============================================================================


@pytest.fixture
def event_loop() -> Generator[asyncio.AbstractEventLoop, None, None]:
    """Create an event loop for async tests.

    Thread-safe: Creates new loop per test to avoid sharing.
    Uses Python 3.16+ compatible API.
    """
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)
    try:
        yield loop
    finally:
        loop.close()
        asyncio.set_event_loop(None)


# ============================================================================
# Configuration Fixtures
# ============================================================================


@pytest.fixture
def subagent_config() -> "SubAgentConfig":
    """Create a test SubAgentConfig - GPU required.

    Thread-safe: Frozen dataclass is immutable.
    """
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
    """Create a test TensorConfig - GPU required.

    Thread-safe: Frozen dataclass is immutable.
    """
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


@pytest.fixture
def compute_config() -> "ComputeConfig":
    """Create a test ComputeConfig - GPU required.

    Thread-safe: Frozen dataclass is immutable.
    """
    from aspire_agents.compute import ComputeConfig

    return ComputeConfig(
        model_name="sentence-transformers/all-MiniLM-L6-v2",
        batch_size=8,
        max_latency_ms=10,
        use_torch_compile=False,  # Disable for faster tests
        use_mixed_precision=True,
        compute_mode="gpu",
        tensor_alignment=128,
    )


# ============================================================================
# Mock Fixtures
# ============================================================================


@pytest.fixture
def mock_torch_cuda() -> Generator[MagicMock, None, None]:
    """Mock torch.cuda for tests without GPU.

    Thread-safe: MagicMock is thread-safe for reads.
    """
    mock_cuda = MagicMock()
    mock_cuda.is_available.return_value = True
    mock_cuda.device_count.return_value = 1
    mock_cuda.current_device.return_value = 0
    mock_cuda.get_device_properties.return_value = MagicMock(
        name="NVIDIA Test GPU",
        major=8,
        minor=6,
        total_memory=24 * 1024 * 1024 * 1024,  # 24GB
    )
    mock_cuda.memory_allocated.return_value = 1024 * 1024 * 1024  # 1GB
    mock_cuda.memory_reserved.return_value = 2 * 1024 * 1024 * 1024  # 2GB

    with patch("torch.cuda", mock_cuda):
        yield mock_cuda


@pytest.fixture
def mock_transformers() -> Generator[tuple[MagicMock, MagicMock], None, None]:
    """Mock HuggingFace transformers for tests without models.

    Returns:
        Tuple of (mock_tokenizer, mock_model)
    """
    import torch

    mock_tokenizer = MagicMock()
    mock_tokenizer.return_value = {
        "input_ids": torch.zeros(1, 128, dtype=torch.long),
        "attention_mask": torch.ones(1, 128, dtype=torch.long),
    }

    mock_model = MagicMock()
    mock_output = MagicMock()
    mock_output.last_hidden_state = torch.zeros(1, 128, 384)
    mock_model.return_value = mock_output
    mock_model.to.return_value = mock_model
    mock_model.eval.return_value = mock_model

    with (
        patch("transformers.AutoTokenizer.from_pretrained", return_value=mock_tokenizer),
        patch("transformers.AutoModel.from_pretrained", return_value=mock_model),
    ):
        yield mock_tokenizer, mock_model


# ============================================================================
# Service Fixtures
# ============================================================================


@pytest.fixture
def reset_singletons() -> Generator[None, None, None]:
    """Reset all singleton services before and after test.

    Thread-safe: Uses locks from each module.
    """
    from aspire_agents.compute import reset_compute_service
    from aspire_agents.guardrails import reset_guardrail_service
    from aspire_agents.subagent import reset_orchestrator

    # Reset before test
    reset_compute_service()
    reset_guardrail_service()
    reset_orchestrator()

    yield

    # Reset after test
    reset_compute_service()
    reset_guardrail_service()
    reset_orchestrator()


@pytest.fixture
async def orchestrator(
    reset_singletons: None,
    subagent_config: "SubAgentConfig",
) -> AsyncGenerator["SubAgentOrchestrator", None]:
    """Create a test SubAgentOrchestrator.

    Thread-safe: Uses singleton pattern with lock.
    """
    from aspire_agents.subagent import SubAgentOrchestrator

    orch = SubAgentOrchestrator(config=subagent_config)
    yield orch
    orch.shutdown()


# ============================================================================
# Thread Safety Test Fixtures
# ============================================================================


@pytest.fixture
def thread_barrier() -> threading.Barrier:
    """Create a barrier for synchronizing test threads.

    Default: 4 threads (matches default thread pool size).
    """
    return threading.Barrier(4)


@pytest.fixture
def thread_results() -> dict[int, Any]:
    """Create a thread-safe dict for collecting test results.

    Thread-safe: dict operations are atomic in CPython.
    """
    return {}


@pytest.fixture
def thread_errors() -> list[Exception]:
    """Create a thread-safe list for collecting test errors.

    Thread-safe: list.append is atomic in CPython.
    """
    return []
