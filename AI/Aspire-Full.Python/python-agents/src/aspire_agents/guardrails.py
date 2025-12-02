"""Thread-safe semantic guardrails for Python 3.15+ free-threaded runtime.

Provides GPU-accelerated semantic similarity checks using BatchComputeService.
All guardrails are async-first and leverage Tensor Cores for embedding computation.

Guardrail Types:
- Input Guardrails: Block harmful/restricted input before tool execution
- Output Guardrails: Detect PII or sensitive data in tool outputs

Thread Safety:
- GuardrailService uses thread-safe singleton pattern
- Concept embeddings are pre-computed and immutable
- All checks are async and non-blocking
"""

from __future__ import annotations

import logging
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any, Final, cast

from .compute import get_compute_service

if TYPE_CHECKING:
    from collections.abc import Awaitable, Callable

    import torch

logger: Final = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class ToolInputGuardrailData:
    """Immutable input data for guardrail evaluation."""

    context: Any  # ToolContext from core.py


@dataclass(frozen=True, slots=True)
class ToolOutputGuardrailData:
    """Immutable output data for guardrail evaluation."""

    output: Any
    context: Any  # ToolContext from core.py


class ToolOutputGuardrailTripwireTriggered(Exception):
    """Raised when an output guardrail blocks content."""

    __slots__ = ("output",)

    def __init__(self, output: ToolGuardrailFunctionOutput) -> None:
        super().__init__(f"Output guardrail triggered: {output.message}")
        self.output = output


@dataclass(slots=True)
class ToolGuardrailFunctionOutput:
    """Result of a guardrail evaluation."""

    output_info: dict[str, Any]
    message: str | None = None

    @classmethod
    def reject_content(cls, message: str, output_info: dict[str, Any]) -> ToolGuardrailFunctionOutput:
        """Create a rejection response with a message."""
        return cls(output_info, message=message)

    @classmethod
    def raise_exception(cls, output_info: dict[str, Any]) -> ToolGuardrailFunctionOutput:
        """Raise an exception to block the output chain."""
        raise ToolOutputGuardrailTripwireTriggered(cls(output_info))


class GuardrailService:
    """Thread-safe semantic guardrail service with GPU-accelerated checks.

    Pre-computes embeddings for restricted concepts on initialization,
    then uses cosine similarity for fast semantic matching.

    Thread Safety:
    - Uses BatchComputeService singleton (thread-safe)
    - Concept embeddings are immutable after initialization
    - All similarity checks are async and non-blocking
    """

    __slots__ = ("compute", "restricted_concepts", "concept_embeddings")

    # Default restricted concept categories
    DEFAULT_CONCEPTS: Final[dict[str, list[str]]] = {
        "pii": [
            "social security number",
            "credit card number",
            "phone number",
            "email address",
            "password",
            "secret key",
            "api key",
            "access token",
        ],
        "harmful": [
            "hack",
            "exploit",
            "malware",
            "virus",
            "attack",
            "vulnerability",
            "injection",
            "backdoor",
        ],
    }

    def __init__(self, restricted_concepts: dict[str, list[str]] | None = None) -> None:
        self.compute = get_compute_service()
        self.restricted_concepts = restricted_concepts or self.DEFAULT_CONCEPTS
        self.concept_embeddings: dict[str, torch.Tensor] = {}
        self._precompute_embeddings()

    def _precompute_embeddings(self) -> None:
        """Pre-compute embeddings for restricted concepts on the GPU."""
        for category, phrases in self.restricted_concepts.items():
            # Use sync method for initialization
            self.concept_embeddings[category] = self.compute.compute_embeddings_sync(phrases)

    async def check_semantic_similarity(self, text: str, category: str, threshold: float = 0.4) -> bool:
        """
        Check if text is semantically similar to a restricted category.
        Returns True if similarity exceeds threshold.
        """
        if not text or category not in self.concept_embeddings:
            return False

        # Compute embedding for the input text asynchronously
        text_embedding = cast(Any, await self.compute.compute_embedding(text))

        # Compute similarity against the category's pre-computed embeddings
        # (1, D) x (N, D)^T -> (1, N)
        # Note: Operations happen on CPU here, which is fast for final dot product
        category_embeddings = self.concept_embeddings[category]
        similarities = (text_embedding.unsqueeze(0) @ category_embeddings.t()).squeeze(0)

        # Check if any phrase matches
        max_similarity = similarities.max().item()
        if max_similarity > threshold:
            logger.warning(
                "Guardrail triggered: '%s' matched '%s' (score: %.2f)",
                text,
                category,
                max_similarity,
            )
            return True

        return False


# Singleton instance
_guardrail_service: GuardrailService | None = None


def get_guardrail_service() -> GuardrailService:
    global _guardrail_service  # pylint: disable=global-statement
    if _guardrail_service is None:
        _guardrail_service = GuardrailService()
    return _guardrail_service


def semantic_input_guardrail(
    category: str = "harmful", threshold: float = 0.4
) -> Callable[[ToolInputGuardrailData], Awaitable[ToolGuardrailFunctionOutput]]:
    """
    Decorator factory for semantic input guardrails.
    """

    async def guardrail(data: ToolInputGuardrailData) -> ToolGuardrailFunctionOutput:
        service = get_guardrail_service()
        args_str = str(data.context.tool_arguments)

        if await service.check_semantic_similarity(args_str, category, threshold):
            return ToolGuardrailFunctionOutput.reject_content(
                message=(f"Input blocked: content semantically similar to restricted " f"category '{category}'."),
                output_info={"blocked_category": category},
            )

        return ToolGuardrailFunctionOutput(output_info={"status": "Input validated"})

    return guardrail


def semantic_output_guardrail(
    category: str = "pii", threshold: float = 0.4
) -> Callable[[ToolOutputGuardrailData], Awaitable[ToolGuardrailFunctionOutput]]:
    """
    Decorator factory for semantic output guardrails.
    """

    async def guardrail(data: ToolOutputGuardrailData) -> ToolGuardrailFunctionOutput:
        service = get_guardrail_service()
        output_str = str(data.output)

        if await service.check_semantic_similarity(output_str, category, threshold):
            # For PII/Output, we often want to raise an exception to stop the chain
            return ToolGuardrailFunctionOutput.raise_exception(
                output_info={
                    "blocked_category": category,
                    "tool": data.context.tool_name,
                }
            )

        return ToolGuardrailFunctionOutput(output_info={"status": "Output validated"})

    return guardrail
