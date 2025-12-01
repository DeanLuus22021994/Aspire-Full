"""Strict local tensor compute service for Aspire Agents."""

import asyncio
import logging
import queue
import threading
import time
from concurrent.futures import Future
from typing import TYPE_CHECKING, Any, List, cast

import torch

from .gpu import ensure_tensor_core_gpu

if TYPE_CHECKING:

    class PreTrainedTokenizerBase:
        def __call__(
            self, text: List[str], padding: bool, truncation: bool, return_tensors: str
        ) -> Any: ...

    class PreTrainedModel(torch.nn.Module):
        def __call__(self, **kwargs: Any) -> Any: ...
        def to(self, *args: Any, **kwargs: Any) -> "PreTrainedModel": ...
        def eval(self) -> "PreTrainedModel": ...

    class AutoTokenizer:
        @staticmethod
        def from_pretrained(model_name: str) -> PreTrainedTokenizerBase: ...

    class AutoModel:
        @staticmethod
        def from_pretrained(model_name: str) -> PreTrainedModel: ...
else:
    from transformers import AutoModel, AutoTokenizer

logger = logging.getLogger(__name__)

# Global lock for thread safety during initialization
_INIT_LOCK = threading.Lock()
_compute_service: "BatchComputeService | None" = None


class TensorCoreUnavailableError(RuntimeError):
    """Raised when strict tensor core requirements are not met."""


class BatchComputeService:
    """
    Provides local tensor compute capabilities using a GPU-resident model.
    Implements dynamic batching and runs in a dedicated thread to leverage
    Python 3.14 free-threading and maximize Tensor Core utilization.
    """

    def __init__(
        self,
        model_name: str = "sentence-transformers/all-MiniLM-L6-v2",
        batch_size: int = 32,
        max_latency_ms: int = 10,
    ):
        self.device = self._enforce_gpu()
        self.batch_size = batch_size
        self.max_latency_ms = max_latency_ms
        self.queue: queue.Queue[tuple[str, Future[torch.Tensor]]] = queue.Queue()
        self.shutdown_event = threading.Event()

        logger.info(
            "Initializing BatchComputeService on %s with model %s",
            self.device,
            model_name,
        )

        try:
            # Load tokenizer and model directly to GPU
            if TYPE_CHECKING:
                self.tokenizer = cast(PreTrainedTokenizerBase, None)
                self.model = cast(PreTrainedModel, None)
            else:
                self.tokenizer = AutoTokenizer.from_pretrained(model_name)
                self.model = AutoModel.from_pretrained(model_name).to(self.device)

            self.model.eval()

            # Optimize model with torch.compile for Python 3.14+ performance
            # Note: This requires a compatible backend. We try/except to be safe.
            try:
                self.model = cast(PreTrainedModel, torch.compile(self.model))
                logger.info(
                    "Model compiled with torch.compile() for maximum efficiency."
                )
            except Exception as e:  # pylint: disable=broad-exception-caught
                logger.warning("Could not compile model: %s. Running in eager mode.", e)

        except Exception as e:  # pylint: disable=broad-exception-caught
            raise TensorCoreUnavailableError(
                f"Failed to load model {model_name} on GPU: {e}"
            ) from e

            # Verify model is actually on GPU (or CPU if fallback enabled)
        # Cast to nn.Module to satisfy type checker since torch.compile returns a callable
        param_device = next(cast(torch.nn.Module, self.model).parameters()).device
        if param_device.type != self.device.type:
            raise TensorCoreUnavailableError(
                f"Model loaded on {param_device} but expected {self.device}!"
            )

        # Start the worker thread
        self.worker_thread = threading.Thread(
            target=self._process_batches, name="TensorComputeWorker", daemon=True
        )
        self.worker_thread.start()

        logger.info("BatchComputeService initialized successfully on Tensor Cores.")

    def _enforce_gpu(self) -> torch.device:
        """Ensure a Tensor Core GPU is available and return the device."""
        import os  # pylint: disable=import-outside-toplevel

        if os.environ.get("ASPIRE_ALLOW_CPU_FALLBACK", "").lower() in ("1", "true"):
            logger.warning("CPU fallback enabled via ASPIRE_ALLOW_CPU_FALLBACK.")
            return torch.device("cpu")

        ensure_tensor_core_gpu()
        if not torch.cuda.is_available():
            raise TensorCoreUnavailableError(
                "CUDA is not available. CPU fallback is strictly forbidden."
            )
        return torch.device("cuda")

    def _process_batches(self):
        """
        Main loop for the worker thread.
        Aggregates requests into batches and executes them.
        """
        batch_texts: List[str] = []
        batch_futures: List[Future[torch.Tensor]] = []
        last_batch_time = time.time()

        while not self.shutdown_event.is_set():
            try:
                # Determine timeout based on max latency
                current_time = time.time()
                time_since_last = (current_time - last_batch_time) * 1000
                remaining_time = max(
                    0.0, (self.max_latency_ms - time_since_last) / 1000.0
                )

                # If we have items and timeout expired, force process
                if batch_texts and remaining_time <= 0:
                    self._execute_batch(batch_texts, batch_futures)
                    batch_texts = []
                    batch_futures = []
                    last_batch_time = time.time()
                    continue

                try:
                    # Wait for next item
                    # If we have items, wait only remaining time
                    # If empty, wait a bit longer (0.1s) to check shutdown
                    if batch_texts:
                        item = self.queue.get(timeout=remaining_time)
                    else:
                        item = self.queue.get(timeout=0.1)

                    batch_texts.append(item[0])
                    batch_futures.append(item[1])

                    # If batch full, process immediately
                    if len(batch_texts) >= self.batch_size:
                        self._execute_batch(batch_texts, batch_futures)
                        batch_texts = []
                        batch_futures = []
                        last_batch_time = time.time()

                except queue.Empty:
                    # Timeout reached, process what we have if any
                    if batch_texts:
                        self._execute_batch(batch_texts, batch_futures)
                        batch_texts = []
                        batch_futures = []
                        last_batch_time = time.time()

            except Exception as e:  # pylint: disable=broad-exception-caught
                logger.error("Error in tensor compute worker: %s", e)
                # Fail all pending futures
                for f in batch_futures:
                    if not f.done():
                        f.set_exception(e)
                batch_texts = []
                batch_futures = []

    def _execute_batch(self, texts: List[str], futures: List[Future[torch.Tensor]]):
        """Run inference on a batch and resolve futures."""
        try:
            # Tokenize
            inputs = self.tokenizer(
                texts,
                padding=True,
                truncation=True,
                return_tensors="pt",
            ).to(self.device)

            # Inference with mixed precision for Tensor Core utilization
            # Only use autocast if on CUDA
            if self.device.type == "cuda":
                with torch.no_grad(), torch.autocast(
                    device_type="cuda", dtype=torch.float16
                ):
                    outputs = self.model(**inputs)
            else:
                with torch.no_grad():
                    outputs = self.model(**inputs)

            # Mean pooling
            attention_mask = cast(torch.Tensor, inputs["attention_mask"])
            token_embeddings = outputs.last_hidden_state
            input_mask_expanded = (
                attention_mask.unsqueeze(-1).expand(token_embeddings.size()).float()
            )

            embeddings = torch.sum(
                token_embeddings * input_mask_expanded, 1
            ) / torch.clamp(input_mask_expanded.sum(1), min=1e-9)

            # Normalize
            embeddings = torch.nn.functional.normalize(embeddings, p=2, dim=1)

            # Move to CPU and resolve futures
            embeddings_cpu = embeddings.cpu()

            for i, future in enumerate(futures):
                if not future.cancelled():
                    future.set_result(embeddings_cpu[i])

        except Exception as e:  # pylint: disable=broad-exception-caught
            for future in futures:
                if not future.cancelled():
                    future.set_exception(e)

    async def compute_embedding(self, text: str) -> torch.Tensor:
        """
        Async method to request an embedding.
        Pushes to queue and awaits the result.
        """
        loop = asyncio.get_running_loop()
        future = loop.create_future()

        # We use a standard concurrent.futures.Future to bridge to the thread
        thread_future: Future[torch.Tensor] = Future()

        def callback(f: Future[torch.Tensor]):
            try:
                result = f.result()
                loop.call_soon_threadsafe(future.set_result, result)
            except Exception as e:  # pylint: disable=broad-exception-caught
                loop.call_soon_threadsafe(future.set_exception, e)

        thread_future.add_done_callback(callback)
        self.queue.put((text, thread_future))

        return await future

    def compute_embeddings_sync(self, texts: List[str]) -> torch.Tensor:
        """
        Synchronous fallback for legacy code or bulk processing.
        Still uses the batching worker to ensure thread safety.
        """
        futures: List[Future[torch.Tensor]] = []
        for text in texts:
            f: Future[torch.Tensor] = Future()
            self.queue.put((text, f))
            futures.append(f)

        results = [f.result() for f in futures]
        return torch.stack(results)


def get_compute_service() -> BatchComputeService:
    """Get or initialize the singleton BatchComputeService."""
    global _compute_service  # pylint: disable=global-statement
    if _compute_service is None:
        with _INIT_LOCK:
            if _compute_service is None:
                _compute_service = BatchComputeService()
    return _compute_service
