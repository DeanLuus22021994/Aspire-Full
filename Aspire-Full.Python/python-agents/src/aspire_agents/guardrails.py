"""
Tensor-native semantic guardrails for Aspire Agents.
Leverages LocalComputeService for high-performance, GPU-accelerated checks.
"""

import logging
from dataclasses import dataclass
from typing import Any, Callable

from .compute import get_compute_service

logger = logging.getLogger(__name__)


@dataclass
class ToolInputGuardrailData:
    context: Any  # ToolContext


@dataclass
class ToolOutputGuardrailData:
    output: Any
    context: Any  # ToolContext


class ToolOutputGuardrailTripwireTriggered(Exception):
    def __init__(self, output: "ToolGuardrailFunctionOutput"):
        self.output = output


class ToolGuardrailFunctionOutput:
    def __init__(self, output_info: dict[str, Any]):
        self.output_info = output_info

    @classmethod
    def reject_content(
        cls, message: str, output_info: dict[str, Any]
    ) -> "ToolGuardrailFunctionOutput":
        # In a real implementation, this would signal rejection to the runner
        return cls(output_info)

    @classmethod
    def raise_exception(
        cls, output_info: dict[str, Any]
    ) -> "ToolGuardrailFunctionOutput":
        raise ToolOutputGuardrailTripwireTriggered(cls(output_info))


class GuardrailService:
    """
    Provides semantic guardrail checks using the local GPU model.
    """

    def __init__(self):
        self.compute = get_compute_service()
        # Pre-compute embeddings for common restricted concepts
        self.restricted_concepts = {
            "pii": [
                "social security number",
                "credit card number",
                "phone number",
                "email address",
                "password",
                "secret key",
            ],
            "harmful": [
                "hack",
                "exploit",
                "malware",
                "virus",
                "attack",
                "vulnerability",
            ],
        }
        self.concept_embeddings = {}
        self._precompute_embeddings()

    def _precompute_embeddings(self):
        """Pre-compute embeddings for restricted concepts on the GPU."""
        for category, phrases in self.restricted_concepts.items():
            self.concept_embeddings[category] = self.compute.compute_embeddings(phrases)

    def check_semantic_similarity(
        self, text: str, category: str, threshold: float = 0.4
    ) -> bool:
        """
        Check if text is semantically similar to a restricted category.
        Returns True if similarity exceeds threshold.
        """
        if not text or category not in self.concept_embeddings:
            return False

        # Compute embedding for the input text
        text_embedding = self.compute.compute_embeddings([text])

        # Compute similarity against the category's pre-computed embeddings
        # (1, D) x (N, D)^T -> (1, N)
        similarities = (text_embedding @ self.concept_embeddings[category].t()).squeeze(
            0
        )

        # Check if any phrase matches
        max_similarity = similarities.max().item()
        if max_similarity > threshold:
            logger.warning(
                f"Guardrail triggered: '{text}' matched '{category}' (score: {max_similarity:.2f})"
            )
            return True

        return False


# Singleton instance
_GUARDRAIL_SERVICE = None


def get_guardrail_service() -> GuardrailService:
    global _GUARDRAIL_SERVICE
    if _GUARDRAIL_SERVICE is None:
        _GUARDRAIL_SERVICE = GuardrailService()
    return _GUARDRAIL_SERVICE


def semantic_input_guardrail(
    category: str = "harmful", threshold: float = 0.4
) -> Callable[[ToolInputGuardrailData], ToolGuardrailFunctionOutput]:
    """
    Decorator factory for semantic input guardrails.
    """

    def guardrail(data: ToolInputGuardrailData) -> ToolGuardrailFunctionOutput:
        service = get_guardrail_service()
        args_str = str(data.context.tool_arguments)

        if service.check_semantic_similarity(args_str, category, threshold):
            return ToolGuardrailFunctionOutput.reject_content(
                message=(
                    f"Input blocked: content semantically similar to restricted "
                    f"category '{category}'."
                ),
                output_info={"blocked_category": category},
            )

        return ToolGuardrailFunctionOutput(output_info={"status": "Input validated"})

    return guardrail


def semantic_output_guardrail(
    category: str = "pii", threshold: float = 0.4
) -> Callable[[ToolOutputGuardrailData], ToolGuardrailFunctionOutput]:
    """
    Decorator factory for semantic output guardrails.
    """

    def guardrail(data: ToolOutputGuardrailData) -> ToolGuardrailFunctionOutput:
        service = get_guardrail_service()
        output_str = str(data.output)

        if service.check_semantic_similarity(output_str, category, threshold):
            # For PII/Output, we often want to raise an exception to stop the chain
            return ToolGuardrailFunctionOutput.raise_exception(
                output_info={
                    "blocked_category": category,
                    "tool": data.context.tool_name,
                }
            )

        return ToolGuardrailFunctionOutput(output_info={"status": "Output validated"})

    return guardrail
