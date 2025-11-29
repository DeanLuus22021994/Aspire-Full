"""Strict local tensor compute service for Aspire Agents."""

import asyncio
import logging
import queue
import threading
import time
from concurrent.futures import Future
from typing import List

import torch
from transformers import AutoModel, AutoTokenizer  # type: ignore

from .gpu import ensure_tensor_core_gpu

logger = logging.getLogger(__name__)

# Global lock for thread safety during initialization
_INIT_LOCK = threading.Lock()
_COMPUTE_SERVICE = None


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
        self.max_latency = max_latency_ms / 1000.0
        self.queue: queue.Queue = queue.Queue()
        self.shutdown_event = threading.Event()

        logger.info(
            f"Initializing BatchComputeService on {self.device} with model {model_name}"
        )

        try:
            # Load tokenizer and model directly to GPU
            self.tokenizer = AutoTokenizer.from_pretrained(model_name)
            self.model = AutoModel.from_pretrained(model_name).to(self.device)
            self.model.eval()

            # Optimize model with torch.compile for Python 3.14+ performance
            # Note: This requires a compatible backend. We try/except to be safe.
            try:
                self.model = torch.compile(self.model)
                logger.info(
                    "Model compiled with torch.compile() for maximum efficiency."
                )
            except Exception as e:
                logger.warning(f"Could not compile model: {e}. Running in eager mode.")

        except Exception as e:
            raise TensorCoreUnavailableError(
                f"Failed to load model {model_name} on GPU: {e}"
            ) from e

        # Verify model is actually on GPU
        if next(self.model.parameters()).device.type != "cuda":  # type: ignore
            raise TensorCoreUnavailableError(
                "Model loaded on CPU despite strict GPU enforcement!"
            )

        # Start the worker thread
        self.worker_thread = threading.Thread(
            target=self._process_batches, name="TensorComputeWorker", daemon=True
        )
        self.worker_thread.start()

        logger.info("BatchComputeService initialized successfully on Tensor Cores.")

    def _enforce_gpu(self) -> torch.device:
        """Ensure a Tensor Core GPU is available and return the device."""
        ensure_tensor_core_gpu()
        if not torch.cuda.is_available():
            raise TensorCoreUnavailableError(
                "CUDA is not available. CPU fallback is strictly forbidden."
            )
        return torch.device("cuda")

    def _process_batches(self):
        """
        Worker loop that consumes requests from the queue, forms batches,
        and executes inference on the GPU.
        """
        batch_texts = []
        batch_futures = []

        while not self.shutdown_event.is_set():
            try:
                # Wait for the first item
                if not batch_texts:
                    item = self.queue.get(timeout=0.1)
                    batch_texts.append(item[0])
                    batch_futures.append(item[1])

                # Try to fill the batch up to max_latency
                start_time = time.time()
                while len(batch_texts) < self.batch_size:
                    remaining_time = self.max_latency - (time.time() - start_time)
                    if remaining_time <= 0:
                        break

                    try:
                        item = self.queue.get(timeout=remaining_time)
                        batch_texts.append(item[0])
                        batch_futures.append(item[1])
                    except queue.Empty:
                        break

                if batch_texts:
                    self._execute_batch(batch_texts, batch_futures)
                    batch_texts = []
                    batch_futures = []

            except queue.Empty:
                continue
            except Exception as e:
                logger.error(f"Error in compute worker: {e}")

    def _execute_batch(self, texts: List[str], futures: List[Future]):
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
            with torch.no_grad(), torch.amp.autocast(
                device_type="cuda", dtype=torch.float16
            ):
                outputs = self.model(**inputs)

            # Mean pooling
            attention_mask = inputs["attention_mask"]
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

        except Exception as e:
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
        thread_future: Future = Future()

        def callback(f):
            try:
                result = f.result()
                loop.call_soon_threadsafe(future.set_result, result)
            except Exception as e:
                loop.call_soon_threadsafe(future.set_exception, e)

        thread_future.add_done_callback(callback)
        self.queue.put((text, thread_future))

        return await future

    def compute_embeddings_sync(self, texts: List[str]) -> torch.Tensor:
        """
        Synchronous fallback for legacy code or bulk processing.
        Still uses the batching worker to ensure thread safety.
        """
        futures = []
        for text in texts:
            f: Future = Future()
            self.queue.put((text, f))
            futures.append(f)

        results = [f.result() for f in futures]
        return torch.stack(results)


def get_compute_service() -> BatchComputeService:
    """Get or initialize the singleton BatchComputeService."""
    global _COMPUTE_SERVICE
    if _COMPUTE_SERVICE is None:
        with _INIT_LOCK:
            if _COMPUTE_SERVICE is None:
                _COMPUTE_SERVICE = BatchComputeService()
    return _COMPUTE_SERVICE
