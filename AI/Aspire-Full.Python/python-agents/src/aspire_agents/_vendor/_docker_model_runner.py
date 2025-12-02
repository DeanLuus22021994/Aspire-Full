"""Docker Model Runner vendor abstractions.

Provides protocol definitions for Docker Model Runner (DMR), enabling type-safe
interaction with the Docker Model Runner API without requiring the docker CLI.

Docker Model Runner makes it easy to manage, run, and deploy AI models using Docker:
- Pull and push models to/from Docker Hub
- Serve models on OpenAI-compatible APIs
- Package GGUF files as OCI Artifacts
- Run and interact with AI models from CLI or API
- GPU acceleration via NVIDIA CUDA, AMD ROCm, Moore Threads MUSA, Huawei CANN

Key Features:
- OpenAI-compatible API endpoint (http://localhost:12434/engines/v1/...)
- Model management (pull, push, list, remove)
- GPU backend support (cuda, rocm, musa, cann, auto, none)
- llama.cpp backend for efficient inference
- OCI artifact packaging for container registries
"""

from __future__ import annotations

import subprocess
from collections.abc import AsyncIterator, Iterator
from dataclasses import dataclass, field
from types import TracebackType
from typing import (
    Any,
    Final,
    Literal,
    Protocol,
    TypeAlias,
    runtime_checkable,
)

from ._enums import GpuBackendType

# ============================================================================
# Type Aliases
# ============================================================================

GpuBackend: TypeAlias = Literal["cuda", "rocm", "musa", "cann", "auto", "none"]
"""Supported GPU backends for Docker Model Runner.

- cuda: NVIDIA CUDA (most common for NVIDIA GPUs)
- rocm: AMD ROCm (for AMD GPUs)
- musa: Moore Threads MUSA
- cann: Huawei CANN
- auto: Automatic detection (may not work correctly)
- none: CPU only
"""

ModelFormat: TypeAlias = Literal["gguf", "onnx", "safetensors"]
"""Supported model file formats."""

RunnerStatus: TypeAlias = Literal["running", "stopped", "starting", "error"]
"""Docker Model Runner status states."""


# ============================================================================
# Exceptions
# ============================================================================


class DockerModelRunnerError(Exception):
    """Base exception for Docker Model Runner operations.

    Raised when Docker Model Runner encounters an error during:
    - Model management operations
    - Runner lifecycle operations
    - GPU configuration
    - API interactions
    """

    pass


class ModelNotFoundError(DockerModelRunnerError):
    """Raised when a requested model is not found locally or in registry."""

    pass


class RunnerNotAvailableError(DockerModelRunnerError):
    """Raised when Docker Model Runner is not running or not installed."""

    pass


class GpuNotAvailableError(DockerModelRunnerError):
    """Raised when GPU acceleration is requested but not available."""

    pass


class ModelPullError(DockerModelRunnerError):
    """Raised when model pull operation fails."""

    pass


class InferenceError(DockerModelRunnerError):
    """Raised when model inference fails."""

    pass


# ============================================================================
# Data Classes
# ============================================================================


@dataclass(frozen=True, slots=True)
class ModelInfo:
    """Information about a Docker Model Runner model.

    Attributes:
        name: Model name/tag (e.g., "ai/qwen3:14B-Q6_K")
        size: Model size in bytes
        format: Model file format
        quantization: Quantization level (e.g., "Q4_K_M", "Q6_K", "F16")
        layers: Number of model layers
        context_size: Maximum context window size
        parameters: Model parameter count (e.g., "7B", "14B", "70B")
        digest: OCI artifact digest
        created_at: ISO timestamp of model creation
    """

    name: str
    size: int
    format: ModelFormat = "gguf"
    quantization: str | None = None
    layers: int | None = None
    context_size: int | None = None
    parameters: str | None = None
    digest: str | None = None
    created_at: str | None = None


@dataclass(frozen=True, slots=True)
class GpuDeviceInfo:
    """GPU device information from Docker Model Runner.

    Attributes:
        index: Device index (0-based)
        name: GPU device name (e.g., "NVIDIA GeForce RTX 4080")
        memory_total: Total GPU memory in bytes
        memory_free: Free GPU memory in bytes
        driver_version: NVIDIA/AMD driver version
        compute_capability: CUDA compute capability (for NVIDIA)
        cuda_version: CUDA version string
    """

    index: int
    name: str
    memory_total: int
    memory_free: int
    driver_version: str | None = None
    compute_capability: tuple[int, int] | None = None
    cuda_version: str | None = None

    @property
    def memory_used(self) -> int:
        """Calculate used memory."""
        return self.memory_total - self.memory_free

    @property
    def memory_utilization(self) -> float:
        """Calculate memory utilization percentage."""
        if self.memory_total == 0:
            return 0.0
        return (self.memory_used / self.memory_total) * 100.0


@dataclass(frozen=True, slots=True)
class RunnerInfo:
    """Docker Model Runner status information.

    Attributes:
        status: Current runner status
        version: Runner version string
        gpu_backend: Active GPU backend
        gpu_devices: List of available GPU devices
        port: API port (default: 12434)
        models_loaded: List of currently loaded models
        llama_cpp_version: Version of llama.cpp backend
    """

    status: RunnerStatus
    version: str | None = None
    gpu_backend: GpuBackend = "none"
    gpu_devices: tuple[GpuDeviceInfo, ...] = field(default_factory=tuple)
    port: int = 12434
    models_loaded: tuple[str, ...] = field(default_factory=tuple)
    llama_cpp_version: str | None = None


@dataclass(frozen=True, slots=True)
class InferenceResult:
    """Result from model inference.

    Attributes:
        content: Generated text content
        model: Model name used for inference
        prompt_tokens: Number of tokens in the prompt
        completion_tokens: Number of tokens generated
        total_tokens: Total tokens processed
        finish_reason: Why generation stopped ("stop", "length", etc.)
        gpu_utilized: Whether GPU was used for inference
        inference_time_ms: Time taken for inference in milliseconds
    """

    content: str
    model: str
    prompt_tokens: int = 0
    completion_tokens: int = 0
    total_tokens: int = 0
    finish_reason: str = "stop"
    gpu_utilized: bool = False
    inference_time_ms: float = 0.0


@dataclass(frozen=True, slots=True)
class StreamChunk:
    """Streaming inference chunk.

    Attributes:
        delta: Incremental text content
        index: Chunk index in stream
        finish_reason: Set when stream completes
    """

    delta: str
    index: int = 0
    finish_reason: str | None = None


@dataclass(slots=True)
class ModelRunnerConfig:
    """Configuration for Docker Model Runner.

    Attributes:
        host: API host (default: "localhost")
        port: API port (default: 12434)
        gpu_backend: GPU backend to use
        context_size: Default context window size
        gpu_layers: Number of layers to offload to GPU (-1 for all)
        batch_size: Batch size for prompt processing
        threads: Number of CPU threads to use
        verbose: Enable verbose logging
    """

    host: str = "localhost"
    port: int = 12434
    gpu_backend: GpuBackend = "cuda"
    context_size: int = 2048
    gpu_layers: int = -1  # -1 means all layers on GPU
    batch_size: int = 512
    threads: int | None = None
    verbose: bool = False

    @property
    def api_base_url(self) -> str:
        """Get the OpenAI-compatible API base URL."""
        return f"http://{self.host}:{self.port}/engines/v1"

    @property
    def model_api_url(self) -> str:
        """Get the model management API URL."""
        return f"http://{self.host}:{self.port}"


# ============================================================================
# Protocols
# ============================================================================


@runtime_checkable
class ModelManagerProtocol(Protocol):
    """Protocol for Docker Model Runner model management.

    Provides operations for managing models:
    - List local models
    - Pull models from registry
    - Push models to registry
    - Remove local models
    - Get model information
    """

    def list_models(self) -> list[ModelInfo]:
        """List all locally available models.

        Returns:
            List of ModelInfo for each local model
        """
        ...

    def pull(
        self,
        model: str,
        *,
        progress_callback: Any | None = None,
    ) -> ModelInfo:
        """Pull a model from Docker Hub or OCI registry.

        Args:
            model: Model name/tag (e.g., "ai/qwen3:14B-Q6_K")
            progress_callback: Optional callback for progress updates

        Returns:
            ModelInfo for the pulled model

        Raises:
            ModelPullError: If pull fails
        """
        ...

    def push(
        self,
        model: str,
        destination: str | None = None,
    ) -> None:
        """Push a model to Docker Hub or OCI registry.

        Args:
            model: Local model name
            destination: Target registry/repository (defaults to source)

        Raises:
            ModelNotFoundError: If model not found locally
        """
        ...

    def remove(self, model: str) -> None:
        """Remove a locally stored model.

        Args:
            model: Model name to remove

        Raises:
            ModelNotFoundError: If model not found
        """
        ...

    def get_info(self, model: str) -> ModelInfo:
        """Get detailed information about a model.

        Args:
            model: Model name

        Returns:
            Detailed ModelInfo

        Raises:
            ModelNotFoundError: If model not found
        """
        ...


@runtime_checkable
class RunnerProtocol(Protocol):
    """Protocol for Docker Model Runner lifecycle management.

    Provides operations for managing the model runner daemon:
    - Start/stop runner
    - Get status
    - Configure GPU backend
    - Check health
    """

    def start(
        self,
        gpu_backend: GpuBackend = "cuda",
    ) -> RunnerInfo:
        """Start the Docker Model Runner.

        Args:
            gpu_backend: GPU backend to use

        Returns:
            RunnerInfo with current status

        Raises:
            GpuNotAvailableError: If requested GPU backend unavailable
        """
        ...

    def stop(self) -> None:
        """Stop the Docker Model Runner."""
        ...

    def restart(self, gpu_backend: GpuBackend | None = None) -> RunnerInfo:
        """Restart the Docker Model Runner.

        Args:
            gpu_backend: Optionally change GPU backend

        Returns:
            RunnerInfo with new status
        """
        ...

    def status(self) -> RunnerInfo:
        """Get current runner status.

        Returns:
            RunnerInfo with current state
        """
        ...

    def reinstall(self, gpu_backend: GpuBackend = "cuda") -> RunnerInfo:
        """Reinstall runner with specified GPU backend.

        Args:
            gpu_backend: GPU backend to configure

        Returns:
            RunnerInfo after reinstallation
        """
        ...


@runtime_checkable
class InferenceProtocol(Protocol):
    """Protocol for model inference operations.

    Provides OpenAI-compatible inference API:
    - Chat completions
    - Streaming responses
    - Embeddings (if supported)
    """

    def chat(
        self,
        model: str,
        messages: list[dict[str, str]],
        *,
        temperature: float = 0.7,
        max_tokens: int | None = None,
        top_p: float = 1.0,
        stream: Literal[False] = False,
    ) -> InferenceResult:
        """Run chat completion (non-streaming).

        Args:
            model: Model name
            messages: List of message dicts with "role" and "content"
            temperature: Sampling temperature
            max_tokens: Maximum tokens to generate
            top_p: Top-p sampling parameter
            stream: Must be False for non-streaming

        Returns:
            InferenceResult with generated content

        Raises:
            InferenceError: If inference fails
            ModelNotFoundError: If model not loaded
        """
        ...

    def chat_stream(
        self,
        model: str,
        messages: list[dict[str, str]],
        *,
        temperature: float = 0.7,
        max_tokens: int | None = None,
        top_p: float = 1.0,
    ) -> Iterator[StreamChunk]:
        """Run streaming chat completion.

        Args:
            model: Model name
            messages: List of message dicts
            temperature: Sampling temperature
            max_tokens: Maximum tokens to generate
            top_p: Top-p sampling parameter

        Yields:
            StreamChunk for each generated token/chunk

        Raises:
            InferenceError: If inference fails
        """
        ...

    async def achat(
        self,
        model: str,
        messages: list[dict[str, str]],
        *,
        temperature: float = 0.7,
        max_tokens: int | None = None,
        top_p: float = 1.0,
    ) -> InferenceResult:
        """Async chat completion.

        Args:
            model: Model name
            messages: List of message dicts
            temperature: Sampling temperature
            max_tokens: Maximum tokens to generate
            top_p: Top-p sampling parameter

        Returns:
            InferenceResult with generated content
        """
        ...

    async def achat_stream(
        self,
        model: str,
        messages: list[dict[str, str]],
        *,
        temperature: float = 0.7,
        max_tokens: int | None = None,
        top_p: float = 1.0,
    ) -> AsyncIterator[StreamChunk]:
        """Async streaming chat completion.

        Args:
            model: Model name
            messages: List of message dicts
            temperature: Sampling temperature
            max_tokens: Maximum tokens to generate
            top_p: Top-p sampling parameter

        Yields:
            StreamChunk for each generated token/chunk
        """
        ...

    def run(
        self,
        model: str,
        prompt: str,
        *,
        temperature: float = 0.7,
        max_tokens: int | None = None,
    ) -> str:
        """Simple single-turn inference (convenience method).

        Args:
            model: Model name
            prompt: User prompt
            temperature: Sampling temperature
            max_tokens: Maximum tokens

        Returns:
            Generated text content
        """
        ...


@runtime_checkable
class DockerModelRunnerProtocol(Protocol):
    """Unified protocol for Docker Model Runner operations.

    Combines model management, runner lifecycle, and inference capabilities
    into a single interface.
    """

    @property
    def models(self) -> ModelManagerProtocol:
        """Access model management operations."""
        ...

    @property
    def runner(self) -> RunnerProtocol:
        """Access runner lifecycle operations."""
        ...

    @property
    def inference(self) -> InferenceProtocol:
        """Access inference operations."""
        ...

    @property
    def config(self) -> ModelRunnerConfig:
        """Get current configuration."""
        ...

    def __enter__(self) -> "DockerModelRunnerProtocol":
        """Enter context manager."""
        ...

    def __exit__(
        self,
        __exc_type: type[BaseException] | None,
        __exc_val: BaseException | None,
        __exc_tb: TracebackType | None,
        /,
    ) -> None:
        """Exit context manager."""
        ...


# ============================================================================
# CLI Wrapper Functions
# ============================================================================

_docker_available: bool = False
_docker_model_available: bool = False


def _check_docker_model() -> bool:
    """Check if docker model command is available."""
    global _docker_model_available
    try:
        result = subprocess.run(
            ["docker", "model", "--help"],
            capture_output=True,
            timeout=5,
        )
        _docker_model_available = result.returncode == 0
    except (subprocess.SubprocessError, FileNotFoundError, OSError):
        _docker_model_available = False
    return _docker_model_available


def is_docker_model_available() -> bool:
    """Check if Docker Model Runner CLI is available.

    Returns:
        True if `docker model` command is available
    """
    global _docker_model_available
    if not _docker_model_available:
        _check_docker_model()
    return _docker_model_available


def get_runner_status() -> RunnerInfo:
    """Get Docker Model Runner status via CLI.

    Returns:
        RunnerInfo with current status

    Raises:
        RunnerNotAvailableError: If runner not available
    """
    if not is_docker_model_available():
        raise RunnerNotAvailableError(
            "Docker Model Runner is not installed. Install Docker Desktop or Docker Engine from official repositories."
        )

    try:
        result = subprocess.run(
            ["docker", "model", "status"],
            capture_output=True,
            text=True,
            timeout=10,
        )
        if result.returncode == 0:
            # Parse status output
            output = result.stdout.lower()
            if "running" in output:
                status: RunnerStatus = "running"
            elif "stopped" in output:
                status = "stopped"
            else:
                status = "error"
            return RunnerInfo(status=status)
        return RunnerInfo(status="stopped")
    except subprocess.SubprocessError as e:
        raise RunnerNotAvailableError(f"Failed to get runner status: {e}") from e


def list_models() -> list[ModelInfo]:
    """List locally available models via CLI.

    Returns:
        List of ModelInfo for each local model

    Raises:
        RunnerNotAvailableError: If runner not available
    """
    if not is_docker_model_available():
        raise RunnerNotAvailableError("Docker Model Runner is not installed.")

    try:
        result = subprocess.run(
            ["docker", "model", "list"],
            capture_output=True,
            text=True,
            timeout=30,
        )
        if result.returncode != 0:
            return []

        models: list[ModelInfo] = []
        lines = result.stdout.strip().split("\n")
        # Skip header line
        for line in lines[1:]:
            if line.strip():
                parts = line.split()
                if parts:
                    name = parts[0]
                    size = int(parts[1]) if len(parts) > 1 and parts[1].isdigit() else 0
                    models.append(ModelInfo(name=name, size=size))
        return models
    except subprocess.SubprocessError:
        return []


def pull_model(model: str) -> ModelInfo:
    """Pull a model from Docker Hub via CLI.

    Args:
        model: Model name/tag (e.g., "ai/qwen3:14B-Q6_K")

    Returns:
        ModelInfo for the pulled model

    Raises:
        ModelPullError: If pull fails
        RunnerNotAvailableError: If runner not available
    """
    if not is_docker_model_available():
        raise RunnerNotAvailableError("Docker Model Runner is not installed.")

    try:
        result = subprocess.run(
            ["docker", "model", "pull", model],
            capture_output=True,
            text=True,
            timeout=3600,  # Models can be large
        )
        if result.returncode != 0:
            raise ModelPullError(f"Failed to pull model {model}: {result.stderr}")
        return ModelInfo(name=model, size=0)  # Size not available from pull output
    except subprocess.TimeoutExpired as e:
        raise ModelPullError(f"Model pull timed out for {model}") from e
    except subprocess.SubprocessError as e:
        raise ModelPullError(f"Failed to pull model {model}: {e}") from e


def run_model(model: str, prompt: str) -> str:
    """Run a model with a prompt via CLI.

    Args:
        model: Model name
        prompt: User prompt

    Returns:
        Generated response text

    Raises:
        InferenceError: If inference fails
        RunnerNotAvailableError: If runner not available
    """
    if not is_docker_model_available():
        raise RunnerNotAvailableError("Docker Model Runner is not installed.")

    try:
        result = subprocess.run(
            ["docker", "model", "run", model, prompt],
            capture_output=True,
            text=True,
            timeout=300,
        )
        if result.returncode != 0:
            raise InferenceError(f"Inference failed: {result.stderr}")
        return result.stdout.strip()
    except subprocess.TimeoutExpired as e:
        raise InferenceError(f"Inference timed out for model {model}") from e
    except subprocess.SubprocessError as e:
        raise InferenceError(f"Inference failed: {e}") from e


def start_runner(gpu_backend: GpuBackend = "cuda") -> RunnerInfo:
    """Start Docker Model Runner via CLI.

    Args:
        gpu_backend: GPU backend to use

    Returns:
        RunnerInfo with new status

    Raises:
        RunnerNotAvailableError: If runner not available
        GpuNotAvailableError: If GPU backend not available
    """
    if not is_docker_model_available():
        raise RunnerNotAvailableError("Docker Model Runner is not installed.")

    try:
        result = subprocess.run(
            ["docker", "model", "start-runner"],
            capture_output=True,
            text=True,
            timeout=60,
        )
        if result.returncode != 0 and "gpu" in result.stderr.lower():
            raise GpuNotAvailableError(
                f"GPU backend '{gpu_backend}' not available: {result.stderr}"
            )
        return get_runner_status()
    except subprocess.SubprocessError as e:
        raise RunnerNotAvailableError(f"Failed to start runner: {e}") from e


def stop_runner() -> None:
    """Stop Docker Model Runner via CLI.

    Raises:
        RunnerNotAvailableError: If runner not available
    """
    if not is_docker_model_available():
        raise RunnerNotAvailableError("Docker Model Runner is not installed.")

    try:
        subprocess.run(
            ["docker", "model", "stop-runner"],
            capture_output=True,
            text=True,
            timeout=30,
        )
    except subprocess.SubprocessError as e:
        raise RunnerNotAvailableError(f"Failed to stop runner: {e}") from e


def reinstall_runner(gpu_backend: GpuBackend = "cuda") -> RunnerInfo:
    """Reinstall Docker Model Runner with GPU support via CLI.

    Args:
        gpu_backend: GPU backend to configure

    Returns:
        RunnerInfo after reinstallation

    Raises:
        RunnerNotAvailableError: If runner not available
    """
    if not is_docker_model_available():
        raise RunnerNotAvailableError("Docker Model Runner is not installed.")

    try:
        # Stop existing runner
        subprocess.run(
            ["docker", "model", "stop-runner"],
            capture_output=True,
            timeout=30,
        )

        # Reinstall with GPU support
        result = subprocess.run(
            ["docker", "model", "reinstall-runner", "--gpu", gpu_backend],
            capture_output=True,
            text=True,
            timeout=120,
        )
        if result.returncode != 0:
            raise RunnerNotAvailableError(
                f"Failed to reinstall runner: {result.stderr}"
            )
        return get_runner_status()
    except subprocess.SubprocessError as e:
        raise RunnerNotAvailableError(f"Failed to reinstall runner: {e}") from e


def get_logs(lines: int = 50) -> str:
    """Get Docker Model Runner logs via CLI.

    Args:
        lines: Number of log lines to retrieve

    Returns:
        Log output string

    Raises:
        RunnerNotAvailableError: If runner not available
    """
    if not is_docker_model_available():
        raise RunnerNotAvailableError("Docker Model Runner is not installed.")

    try:
        result = subprocess.run(
            ["docker", "model", "logs"],
            capture_output=True,
            text=True,
            timeout=10,
        )
        output = result.stdout if result.returncode == 0 else ""
        log_lines = output.strip().split("\n")
        return "\n".join(log_lines[-lines:])
    except subprocess.SubprocessError:
        return ""


def check_gpu_access() -> bool:
    """Check if GPU is accessible from Docker Model Runner.

    Verifies GPU access by checking nvidia-smi inside the runner container.

    Returns:
        True if GPU is accessible
    """
    try:
        result = subprocess.run(
            ["docker", "exec", "docker-model-runner", "nvidia-smi"],
            capture_output=True,
            timeout=10,
        )
        return result.returncode == 0
    except subprocess.SubprocessError:
        return False


# ============================================================================
# OpenAI-Compatible API Constants
# ============================================================================

# Default API endpoints
DEFAULT_API_HOST: Final[str] = "localhost"
DEFAULT_API_PORT: Final[int] = 12434
DEFAULT_API_BASE: Final[str] = f"http://{DEFAULT_API_HOST}:{DEFAULT_API_PORT}/engines/v1"

# Model endpoints
CHAT_COMPLETIONS_ENDPOINT: Final[str] = "/chat/completions"
MODELS_ENDPOINT: Final[str] = "/models"
EMBEDDINGS_ENDPOINT: Final[str] = "/embeddings"


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Type Aliases
    "GpuBackend",
    "ModelFormat",
    "RunnerStatus",
    # Enums
    "GpuBackendType",
    # Exceptions
    "DockerModelRunnerError",
    "ModelNotFoundError",
    "RunnerNotAvailableError",
    "GpuNotAvailableError",
    "ModelPullError",
    "InferenceError",
    # Data Classes
    "ModelInfo",
    "GpuDeviceInfo",
    "RunnerInfo",
    "InferenceResult",
    "StreamChunk",
    "ModelRunnerConfig",
    # Protocols
    "ModelManagerProtocol",
    "RunnerProtocol",
    "InferenceProtocol",
    "DockerModelRunnerProtocol",
    # CLI Functions
    "is_docker_model_available",
    "get_runner_status",
    "list_models",
    "pull_model",
    "run_model",
    "start_runner",
    "stop_runner",
    "reinstall_runner",
    "get_logs",
    "check_gpu_access",
    # API Constants
    "DEFAULT_API_HOST",
    "DEFAULT_API_PORT",
    "DEFAULT_API_BASE",
    "CHAT_COMPLETIONS_ENDPOINT",
    "MODELS_ENDPOINT",
    "EMBEDDINGS_ENDPOINT",
]
