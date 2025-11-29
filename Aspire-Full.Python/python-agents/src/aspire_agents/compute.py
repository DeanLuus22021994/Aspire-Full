"""Strict local tensor compute service for Aspire Agents."""

import logging
import threading

import torch
from transformers import AutoModel, AutoTokenizer  # type: ignore

from .gpu import ensure_tensor_core_gpu

logger = logging.getLogger(__name__)

# Global lock for thread safety during initialization, though inference is thread-safe
_INIT_LOCK = threading.Lock()
_COMPUTE_SERVICE = None


class TensorCoreUnavailableError(RuntimeError):
    """Raised when strict tensor core requirements are not met."""


class LocalComputeService:
    """
    Provides local tensor compute capabilities using a GPU-resident model.
    Strictly enforces CUDA usage and fails if CPU fallback is attempted.
    """

    def __init__(self, model_name: str = "sentence-transformers/all-MiniLM-L6-v2"):
        self.device = self._enforce_gpu()
        logger.info(
            f"Initializing LocalComputeService on {self.device} with model {model_name}"
        )

        try:
            # Load tokenizer and model directly to GPU
            self.tokenizer = AutoTokenizer.from_pretrained(model_name)
            self.model = AutoModel.from_pretrained(model_name).to(self.device)
            self.model.eval()  # Set to evaluation mode
        except Exception as e:
            raise TensorCoreUnavailableError(
                f"Failed to load model {model_name} on GPU: {e}"
            ) from e

        # Verify model is actually on GPU
        if next(self.model.parameters()).device.type != "cuda":
            raise TensorCoreUnavailableError(
                "Model loaded on CPU despite strict GPU enforcement!"
            )

        logger.info("LocalComputeService initialized successfully on Tensor Cores.")

    def _enforce_gpu(self) -> torch.device:
        """Ensure a Tensor Core GPU is available and return the device."""
        # This raises if no suitable GPU is found
        ensure_tensor_core_gpu()

        if not torch.cuda.is_available():
            raise TensorCoreUnavailableError(
                "CUDA is not available. CPU fallback is strictly forbidden."
            )

        return torch.device("cuda")

    def compute_embeddings(self, texts: list[str]) -> torch.Tensor:
        """
        Compute embeddings for a list of texts using the GPU.
        This method is thread-safe and releases the GIL where possible in Python 3.14.
        """
        if not texts:
            return torch.empty(0, device=self.device)

        # Tokenize
        inputs = self.tokenizer(
            texts,
            padding=True,
            truncation=True,
            return_tensors="pt",
        ).to(self.device)

        # Inference (no_grad to save memory and computation)
        with torch.no_grad():
            outputs = self.model(**inputs)

        # Mean pooling
        attention_mask = inputs["attention_mask"]
        token_embeddings = outputs.last_hidden_state
        input_mask_expanded = (
            attention_mask.unsqueeze(-1).expand(token_embeddings.size()).float()
        )

        embeddings = torch.sum(token_embeddings * input_mask_expanded, 1) / torch.clamp(
            input_mask_expanded.sum(1), min=1e-9
        )

        # Normalize
        embeddings = torch.nn.functional.normalize(embeddings, p=2, dim=1)

        return embeddings

    def compute_similarity(self, text: str, candidates: list[str]) -> list[float]:
        """Compute cosine similarity between a text and a list of candidates."""
        if not candidates:
            return []

        all_texts = [text] + candidates
        embeddings = self.compute_embeddings(all_texts)

        query_embedding = embeddings[0].unsqueeze(0)
        candidate_embeddings = embeddings[1:]

        # Cosine similarity
        similarities = torch.mm(query_embedding, candidate_embeddings.transpose(0, 1))
        return similarities.squeeze(0).tolist()


def get_compute_service() -> LocalComputeService:
    """Get or initialize the singleton LocalComputeService."""
    global _COMPUTE_SERVICE
    if _COMPUTE_SERVICE is None:
        with _INIT_LOCK:
            if _COMPUTE_SERVICE is None:
                _COMPUTE_SERVICE = LocalComputeService()
    return _COMPUTE_SERVICE
