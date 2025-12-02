"""Thread-safe tensor compute service for Python 3.16+ free-threaded runtime.

This module provides a BatchComputeService that leverages:
- Python 3.16 free-threading (PYTHON_GIL=0) for true parallelism
- NVIDIA Tensor Cores via torch.autocast with float16 precision
- Dynamic batching with configurable latency bounds
- Thread-safe singleton pattern using threading.Lock

ALL COMPUTE IS GPU-ONLY. NO CPU FALLBACK.

Environment Variables (from Dockerfile):
- ASPIRE_TENSOR_BATCH_SIZE: Batch size for tensor ops (default: 32)
- ASPIRE_COMPUTE_MODE: Compute mode - gpu only (default: gpu)
- CUDA_TENSOR_CORE_ALIGNMENT: Memory alignment in bytes (default: 128)
- PYTORCH_CUDA_ALLOC_CONF: PyTorch memory allocator config
"""

from __future__ import annotations

import asyncio
import logging
import os
import queue
import sys
import threading
import time
from collections.abc import Sequence
from concurrent.futures import Future
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any, Final, Protocol, cast

import torch

from ._typing import compile_model, get_gil_status_string
from .gpu import ensure_tensor_core_gpu

if TYPE_CHECKING:

    # pylint: disable=too-few-public-methods,missing-class-docstring,missing-function-docstring

    class PreTrainedTokenizerBase(Protocol):
        """Protocol for HuggingFace tokenizer interface."""

        def __call__(
            self,
            text: str | Sequence[str],
            padding: bool | str = ...,
            truncation: bool | str = ...,
            max_length: int | None = ...,
            return_tensors: str | None = ...,
            **kwargs: Any,
        ) -> Any:
            """Tokenize input text."""
            ...

    class PreTrainedModel(Protocol):
        """Protocol for HuggingFace model interface."""

        def __call__(self, **kwargs: Any) -> Any:
            """Run forward pass."""
            ...

        def to(self, *args: Any, **kwargs: Any) -> "PreTrainedModel":
            """Move model to device."""
            ...

        def eval(self) -> "PreTrainedModel":
            """Set model to evaluation mode."""
            ...

        def parameters(self) -> Any:
            """Return model parameters."""
            ...

    class AutoTokenizer:
        """Stub for HuggingFace AutoTokenizer."""

        @staticmethod
        def from_pretrained(_model_name: str) -> PreTrainedTokenizerBase:
            """Load pretrained tokenizer."""
            ...

    class AutoModel:
        """Stub for HuggingFace AutoModel."""

        @staticmethod
        def from_pretrained(_model_name: str) -> PreTrainedModel:
            """Load pretrained model."""
            ...

    # pylint: enable=too-few-public-methods,missing-class-docstring,missing-function-docstring
else:
    from transformers import AutoModel, AutoTokenizer

logger: Final = logging.getLogger(__name__)

# Thread-safe singleton pattern for Python 3.15 free-threaded
# Using a dedicated lock since __init__ may run concurrently without GIL
_INIT_LOCK: Final = threading.Lock()
_compute_service: BatchComputeService | None = None

# Environment variable configuration
_ASPIRE_COMPUTE_MODE: Final[str] = os.environ.get("ASPIRE_COMPUTE_MODE", "gpu")
_ASPIRE_TENSOR_BATCH_SIZE: Final[int] = int(os.environ.get("ASPIRE_TENSOR_BATCH_SIZE", "32"))
_CUDA_TENSOR_CORE_ALIGNMENT: Final[int] = int(os.environ.get("CUDA_TENSOR_CORE_ALIGNMENT", "128"))


@dataclass(frozen=True, slots=True)
class ComputeConfig:
    """Immutable configuration for tensor compute service.

    GPU-ONLY. No CPU fallback allowed.
    Default values are read from environment variables set in Dockerfiles.

    Attributes:
        model_name: HuggingFace model for embeddings
        batch_size: Batch size from ASPIRE_TENSOR_BATCH_SIZE
        max_latency_ms: Maximum latency before batch processing
        use_torch_compile: Enable torch.compile optimization
        use_mixed_precision: Enable FP16/BF16 mixed precision
        compute_mode: Always 'gpu' - no CPU fallback
        tensor_alignment: From CUDA_TENSOR_CORE_ALIGNMENT (default: 128)
    """

    model_name: str = "sentence-transformers/all-MiniLM-L6-v2"
    batch_size: int = field(default_factory=lambda: _ASPIRE_TENSOR_BATCH_SIZE)
    max_latency_ms: int = 10
    use_torch_compile: bool = True
    use_mixed_precision: bool = True
    compute_mode: str = "gpu"  # GPU-only, no CPU fallback
    tensor_alignment: int = field(default_factory=lambda: _CUDA_TENSOR_CORE_ALIGNMENT)

    @classmethod
    def from_env(cls) -> ComputeConfig:
        """Create ComputeConfig from environment variables.

        Returns:
            ComputeConfig with environment-based defaults
        """
        return cls()

    @property
    def uses_gpu(self) -> bool:
        """GPU is always required - no CPU fallback."""
        return True


class TensorCoreUnavailableError(RuntimeError):
    """Raised when strict tensor core requirements are not met."""


class BatchComputeService:
    """Thread-safe tensor compute service for Python 3.15+ free-threaded runtime.

    Provides GPU-accelerated embeddings with:
    - Dynamic batching (configurable batch size and latency)
    - Tensor Core acceleration via torch.autocast(float16)
    - torch.compile() optimization for maximum throughput
    - Thread-safe queue-based request handling

    The service runs a dedicated worker thread that aggregates requests
    into batches, maximizing GPU utilization while respecting latency bounds.

    Attributes:
        config: Immutable configuration for the compute service
        device: torch.device (cuda only - GPU required)
        model: Pre-trained transformer model (compiled if supported)
        tokenizer: HuggingFace tokenizer for text encoding
    """

    __slots__ = (
        "config",
        "device",
        "tokenizer",
        "model",
        "_queue",
        "_shutdown_event",
        "_worker_thread",
        "_stats_lock",
        "_total_requests",
        "_total_batches",
    )

    def __init__(
        self,
        config: ComputeConfig | None = None,
        *,
        model_name: str | None = None,
        batch_size: int | None = None,
        max_latency_ms: int | None = None,
    ) -> None:
        # Support both config object and legacy kwargs
        if config is not None:
            self.config = config
        else:
            self.config = ComputeConfig(
                model_name=model_name or "sentence-transformers/all-MiniLM-L6-v2",
                batch_size=batch_size or 32,
                max_latency_ms=max_latency_ms or 10,
            )

        super().__init__()
        self.device = self._enforce_gpu()
        self._queue: queue.Queue[tuple[str, Future[torch.Tensor]]] = queue.Queue()
        self._shutdown_event = threading.Event()
        self._stats_lock = threading.Lock()
        self._total_requests = 0
        self._total_batches = 0

        logger.info(
            "Initializing BatchComputeService on %s with model %s (Python %s.%s, GIL=%s)",
            self.device,
            self.config.model_name,
            sys.version_info.major,
            sys.version_info.minor,
            get_gil_status_string(),
        )

        try:
            # Load tokenizer and model directly to GPU
            if TYPE_CHECKING:
                self.tokenizer = cast(PreTrainedTokenizerBase, None)
                self.model = cast(PreTrainedModel, None)
            else:
                self.tokenizer = AutoTokenizer.from_pretrained(self.config.model_name)
                self.model = AutoModel.from_pretrained(self.config.model_name).to(self.device)

            self.model.eval()

            # Optimize model with torch.compile for Python 3.16+ free-threaded performance
            # torch.compile provides significant speedups on modern GPUs with Tensor Cores
            if self.config.use_torch_compile:
                try:
                    # Use 'reduce-overhead' mode for best latency in batched inference
                    self.model = cast(
                        PreTrainedModel,
                        compile_model(
                            self.model,
                            mode="reduce-overhead",
                            fullgraph=False,  # Allow graph breaks for HuggingFace models
                        ),
                    )
                    logger.info("Model compiled with torch.compile(mode='reduce-overhead') for Tensor Core efficiency.")
                except Exception as e:  # pylint: disable=broad-exception-caught
                    logger.warning("Could not compile model: %s. Running in eager mode.", e)

        except Exception as e:  # pylint: disable=broad-exception-caught
            raise TensorCoreUnavailableError(f"Failed to load model {self.config.model_name} on GPU: {e}") from e

        # Verify model is on GPU - use getattr for type-safe access
        params_fn = getattr(self.model, "parameters")
        first_param: object = next(params_fn())
        param_device: torch.device = getattr(first_param, "device")
        if param_device.type != self.device.type:
            raise TensorCoreUnavailableError(f"Model loaded on {param_device} but expected {self.device}!")

        # Start the worker thread with high priority for real-time batching
        self._worker_thread = threading.Thread(
            target=self._process_batches,
            name="TensorComputeWorker-FreeThreaded",
            daemon=True,
        )
        self._worker_thread.start()

        logger.info(
            "BatchComputeService initialized on Tensor Cores (batch_size=%d, max_latency=%dms).",
            self.config.batch_size,
            self.config.max_latency_ms,
        )

    def _enforce_gpu(self) -> torch.device:
        """Ensure a Tensor Core GPU is available - NO CPU FALLBACK."""
        if not torch.cuda.is_available():
            raise TensorCoreUnavailableError(
                "CUDA GPU required. NO CPU FALLBACK. " + "Ensure NVIDIA GPU is available with proper drivers."
            )
        ensure_tensor_core_gpu()
        return torch.device("cuda")

    def _process_batches(self) -> None:
        """Main loop for the worker thread - thread-safe batch aggregation.

        This method runs in a dedicated thread and aggregates incoming requests
        into batches based on batch_size and max_latency_ms constraints.

        Thread Safety (Python 3.15 free-threaded):
        - Queue operations are inherently thread-safe
        - Stats updates use a dedicated lock
        - No shared mutable state beyond the queue
        """
        batch_texts: list[str] = []
        batch_futures: list[Future[torch.Tensor]] = []
        last_batch_time = time.perf_counter()  # Higher precision timer

        while not self._shutdown_event.is_set():
            try:
                # Determine timeout based on max latency
                current_time = time.perf_counter()
                time_since_last = (current_time - last_batch_time) * 1000
                remaining_time = max(0.0, (self.config.max_latency_ms - time_since_last) / 1000.0)

                # If we have items and timeout expired, force process
                if batch_texts and remaining_time <= 0:
                    self._execute_batch(batch_texts, batch_futures)
                    batch_texts = []
                    batch_futures = []
                    last_batch_time = time.perf_counter()
                    continue

                try:
                    # Wait for next item
                    # If we have items, wait only remaining time
                    # If empty, wait a bit longer (0.1s) to check shutdown
                    if batch_texts:
                        item = self._queue.get(timeout=remaining_time)
                    else:
                        item = self._queue.get(timeout=0.1)

                    batch_texts.append(item[0])
                    batch_futures.append(item[1])

                    # If batch full, process immediately
                    if len(batch_texts) >= self.config.batch_size:
                        self._execute_batch(batch_texts, batch_futures)
                        batch_texts = []
                        batch_futures = []
                        last_batch_time = time.perf_counter()

                except queue.Empty:
                    # Timeout reached, process what we have if any
                    if batch_texts:
                        self._execute_batch(batch_texts, batch_futures)
                        batch_texts = []
                        batch_futures = []
                        last_batch_time = time.perf_counter()

            except Exception as e:  # pylint: disable=broad-exception-caught
                logger.error("Error in tensor compute worker: %s", e)
                # Fail all pending futures
                for f in batch_futures:
                    if not f.done():
                        f.set_exception(e)
                batch_texts = []
                batch_futures = []

    def _execute_batch(self, texts: list[str], futures: list[Future[torch.Tensor]]) -> None:
        """Run inference on a batch and resolve futures - Tensor Core optimized.

        Uses torch.autocast with float16 for Tensor Core acceleration.
        Mean pooling is used for sentence embeddings, followed by L2 normalization.
        """
        # Update stats (thread-safe)
        with self._stats_lock:
            self._total_batches += 1
            self._total_requests += len(texts)

        try:
            # Tokenize with padding/truncation for uniform batch processing
            inputs = self.tokenizer(
                texts,
                padding=True,
                truncation=True,
                max_length=512,  # Explicit limit for efficiency
                return_tensors="pt",
            ).to(self.device)

            # Inference with mixed precision for Tensor Core utilization
            # torch.autocast enables TF32/FP16 matrix ops on Ampere+ GPUs
            # GPU-ONLY: always use CUDA autocast
            with torch.no_grad(), torch.autocast(device_type="cuda", dtype=torch.float16):
                outputs: object = self.model(**inputs)

            # Mean pooling over token embeddings (masked)
            attention_mask = cast(torch.Tensor, inputs["attention_mask"])
            token_embeddings: torch.Tensor = getattr(outputs, "last_hidden_state")
            input_mask_expanded = attention_mask.unsqueeze(-1).expand(token_embeddings.size()).float()

            embeddings = torch.sum(token_embeddings * input_mask_expanded, 1) / torch.clamp(
                input_mask_expanded.sum(1), min=1e-9
            )

            # L2 normalization for cosine similarity
            embeddings = torch.nn.functional.normalize(embeddings, p=2, dim=1)

            # Transfer to CPU for interop (non-blocking for overlapped compute)
            embeddings_cpu = embeddings.cpu()

            # Resolve futures - thread-safe via Future's internal locking
            for i, future in enumerate(futures):
                if not future.cancelled():
                    future.set_result(embeddings_cpu[i])

        except Exception as e:  # pylint: disable=broad-exception-caught
            logger.error("Batch execution failed: %s", e)
            for future in futures:
                if not future.cancelled():
                    future.set_exception(e)

    async def compute_embedding(self, text: str) -> torch.Tensor:
        """Compute embedding for a single text asynchronously.

        Thread-safe: uses queue + Future to bridge async/threaded worlds.
        The request is batched with others for optimal GPU utilization.

        Args:
            text: Input text to embed

        Returns:
            Normalized embedding tensor (1D, on CPU)
        """
        loop = asyncio.get_running_loop()
        future = loop.create_future()

        # Bridge concurrent.futures.Future to asyncio.Future (thread-safe)
        thread_future: Future[torch.Tensor] = Future()

        def callback(f: Future[torch.Tensor]) -> None:
            try:
                result = f.result()
                loop.call_soon_threadsafe(future.set_result, result)
            except Exception as e:  # pylint: disable=broad-exception-caught
                loop.call_soon_threadsafe(future.set_exception, e)

        thread_future.add_done_callback(callback)
        self._queue.put((text, thread_future))

        return await future

    async def compute_embeddings(self, texts: list[str]) -> torch.Tensor:
        """Compute embeddings for multiple texts asynchronously.

        All texts are submitted to the batching queue and awaited together.
        More efficient than calling compute_embedding in a loop.

        Args:
            texts: List of input texts to embed

        Returns:
            Stacked embedding tensor (2D: [N, D], on CPU)
        """
        tasks = [self.compute_embedding(text) for text in texts]
        results = await asyncio.gather(*tasks)
        return torch.stack(results)

    def compute_embeddings_sync(self, texts: list[str]) -> torch.Tensor:
        """Synchronous batch embedding computation.

        Thread-safe: uses the same batching worker as async methods.
        Useful for initialization or non-async contexts.

        Args:
            texts: List of input texts to embed

        Returns:
            Stacked embedding tensor (2D: [N, D], on CPU)
        """
        futures: list[Future[torch.Tensor]] = []
        for text in texts:
            f: Future[torch.Tensor] = Future()
            self._queue.put((text, f))
            futures.append(f)

        results = [f.result() for f in futures]
        return torch.stack(results)

    def get_stats(self) -> dict[str, int | float]:
        """Get compute service statistics (thread-safe)."""
        with self._stats_lock:
            return {
                "total_requests": self._total_requests,
                "total_batches": self._total_batches,
                "avg_batch_size": self._total_requests / self._total_batches if self._total_batches > 0 else 0.0,
                "queue_size": self._queue.qsize(),
            }

    def shutdown(self) -> None:
        """Gracefully shutdown the compute worker thread."""
        self._shutdown_event.set()
        if self._worker_thread.is_alive():
            self._worker_thread.join(timeout=5.0)


def get_compute_service(config: ComputeConfig | None = None) -> BatchComputeService:
    """Get or initialize the singleton BatchComputeService.

    Thread-safe: uses double-checked locking pattern suitable for
    Python 3.15 free-threaded runtime (PYTHON_GIL=0).

    Args:
        config: Optional configuration. Only used on first initialization.

    Returns:
        The singleton BatchComputeService instance.
    """
    global _compute_service  # pylint: disable=global-statement
    if _compute_service is None:
        with _INIT_LOCK:
            if _compute_service is None:
                _compute_service = BatchComputeService(config=config)
    return _compute_service


def reset_compute_service() -> None:
    """Reset the singleton (for testing only)."""
    global _compute_service  # pylint: disable=global-statement
    with _INIT_LOCK:
        if _compute_service is not None:
            _compute_service.shutdown()
            _compute_service = None
