"""Thread-safe semantic guardrails for Python 3.15+ free-threaded runtime.

Provides GPU-accelerated semantic similarity checks using BatchComputeService.
All guardrails are async-first and leverage Tensor Cores for embedding computation.

Guardrail Types:
- Input Guardrails: Block harmful/restricted input before tool execution
- Output Guardrails: Detect PII or sensitive data in tool outputs

Thread Safety:
- GuardrailService uses thread-safe singleton pattern with locking
- Concept embeddings are pre-computed and immutable after initialization
- All similarity checks are async and non-blocking

Usage:
    >>> @function_tool
    ... async def my_tool(arg: str) -> str:
    ...     return f"Result: {arg}"
    >>> my_tool.tool_input_guardrails.append(semantic_input_guardrail("harmful"))
"""

from __future__ import annotations

import logging
import threading
from dataclasses import dataclass
from typing import TYPE_CHECKING, Any, Final, Unpack

import torch

from .compute import get_compute_service

if TYPE_CHECKING:
    from collections.abc import Awaitable, Callable

    from ._kwargs import GuardrailKwargs

logger: Final[logging.Logger] = logging.getLogger(__name__)

# Thread-safe singleton lock
_GUARDRAIL_LOCK: Final[threading.Lock] = threading.Lock()
_guardrail_service: GuardrailService | None = None


@dataclass(frozen=True, slots=True)
class ToolInputGuardrailData:
    """Immutable input data for guardrail evaluation.

    Thread-safe via frozen dataclass.

    Attributes:
        context: Tool context with name and arguments
    """

    context: Any  # ToolContext from core.py


@dataclass(frozen=True, slots=True)
class ToolOutputGuardrailData:
    """Immutable output data for guardrail evaluation.

    Thread-safe via frozen dataclass.

    Attributes:
        output: The tool's output value
        context: Tool context with name and arguments
    """

    output: Any
    context: Any  # ToolContext from core.py


class ToolOutputGuardrailTripwireTriggered(Exception):
    """Raised when an output guardrail blocks content.

    This exception should be caught at the agent runner level
    to prevent sensitive data from being returned to the user.
    """

    __slots__ = ("output",)

    def __init__(self, output: ToolGuardrailFunctionOutput) -> None:
        super().__init__(f"Output guardrail triggered: {output.message}")
        self.output = output


@dataclass(slots=True)
class ToolGuardrailFunctionOutput:
    """Result of a guardrail evaluation.

    Not frozen to allow modification during evaluation, but
    individual instances should be treated as effectively immutable
    after creation.

    Attributes:
        output_info: Metadata about the guardrail decision
        message: Human-readable message (present if blocked)
    """

    output_info: dict[str, Any]
    message: str | None = None

    @classmethod
    def allow(cls, info: dict[str, Any] | None = None) -> ToolGuardrailFunctionOutput:
        """Create an allow response (no blocking)."""
        return cls(output_info=info or {"status": "allowed"}, message=None)

    @classmethod
    def reject_content(
        cls,
        message: str,
        output_info: dict[str, Any],
    ) -> ToolGuardrailFunctionOutput:
        """Create a rejection response with a message."""
        return cls(output_info=output_info, message=message)

    @classmethod
    def raise_exception(cls, output_info: dict[str, Any]) -> ToolGuardrailFunctionOutput:
        """Raise an exception to block the output chain.

        This should be used for output guardrails where we want to
        prevent the output from ever being returned.

        Raises:
            ToolOutputGuardrailTripwireTriggered: Always
        """
        raise ToolOutputGuardrailTripwireTriggered(cls(output_info))


class GuardrailService:
    """Thread-safe semantic guardrail service with GPU-accelerated checks.

    Pre-computes embeddings for restricted concepts on initialization,
    then uses cosine similarity for fast semantic matching.

    Thread Safety:
    - Uses BatchComputeService singleton (thread-safe)
    - Concept embeddings are immutable after initialization
    - All similarity checks are async and non-blocking

    Attributes:
        compute: BatchComputeService for embedding computation
        restricted_concepts: Mapping of category -> list of phrases
        concept_embeddings: Pre-computed embeddings per category
    """

    __slots__ = ("compute", "restricted_concepts", "concept_embeddings", "_initialized")

    # Default restricted concept categories - comprehensive list
    DEFAULT_CONCEPTS: Final[dict[str, tuple[str, ...]]] = {
        "pii": (
            "social security number",
            "credit card number",
            "phone number",
            "email address",
            "password",
            "secret key",
            "api key",
            "access token",
            "private key",
            "bank account number",
            "driver license",
            "passport number",
            "date of birth",
            "home address",
            "medical record",
        ),
        "harmful": (
            "hack",
            "exploit",
            "malware",
            "virus",
            "attack",
            "vulnerability",
            "injection",
            "backdoor",
            "ransomware",
            "phishing",
            "keylogger",
            "rootkit",
            "zero day",
            "buffer overflow",
            "privilege escalation",
        ),
        "illegal": (
            "illegal drugs",
            "weapons trafficking",
            "money laundering",
            "fraud",
            "identity theft",
            "child exploitation",
            "terrorism",
            "human trafficking",
        ),
    }

    def __init__(
        self,
        restricted_concepts: dict[str, tuple[str, ...] | list[str]] | None = None,
    ) -> None:
        """Initialize guardrail service with concept embeddings.

        Args:
            restricted_concepts: Custom categories and phrases to block.
                                Defaults to DEFAULT_CONCEPTS if not provided.
        """
        super().__init__()
        self.compute = get_compute_service()
        self._initialized = False
        self.concept_embeddings: dict[str, torch.Tensor] = {}

        # Normalize to tuple for immutability
        if restricted_concepts is None:
            self.restricted_concepts = self.DEFAULT_CONCEPTS
        else:
            self.restricted_concepts = {
                k: tuple(v) if isinstance(v, list) else v for k, v in restricted_concepts.items()
            }

        self._precompute_embeddings()
        self._initialized = True

    def _precompute_embeddings(self) -> None:
        """Pre-compute embeddings for restricted concepts on the GPU.

        Uses sync method since this runs during initialization.
        """
        for category, phrases in self.restricted_concepts.items():
            phrase_list = list(phrases)  # Convert tuple to list for compute
            self.concept_embeddings[category] = self.compute.compute_embeddings_sync(phrase_list)
            logger.debug(
                "Pre-computed %d embeddings for category '%s'",
                len(phrases),
                category,
            )

    async def check_semantic_similarity(
        self,
        text: str,
        category: str,
        threshold: float = 0.4,
    ) -> tuple[bool, float]:
        """Check if text is semantically similar to a restricted category.

        Args:
            text: Input text to check
            category: Category name to check against
            threshold: Similarity threshold (0.0-1.0, default 0.4)

        Returns:
            Tuple of (is_blocked, max_similarity_score)
        """
        if not text or category not in self.concept_embeddings:
            return False, 0.0

        # Compute embedding for the input text asynchronously
        text_embedding = await self.compute.compute_embedding(text)

        # Compute cosine similarity against the category's pre-computed embeddings
        # Shape: (1, D) @ (N, D).T -> (1, N) -> squeeze -> (N,)
        category_embeddings = self.concept_embeddings[category]

        # Tensor operations - both are already torch.Tensor
        similarities = (text_embedding.unsqueeze(0) @ category_embeddings.t()).squeeze(0)
        max_similarity: float = similarities.max().item()

        if max_similarity > threshold:
            logger.warning(
                "Guardrail triggered: text matched '%s' category (score: %.3f > %.3f)",
                category,
                max_similarity,
                threshold,
            )
            return True, max_similarity

        return False, max_similarity

    async def check_all_categories(
        self,
        text: str,
        threshold: float = 0.4,
    ) -> dict[str, tuple[bool, float]]:
        """Check text against all registered categories.

        Args:
            text: Input text to check
            threshold: Similarity threshold for all categories

        Returns:
            Dictionary mapping category -> (is_blocked, score)
        """
        results: dict[str, tuple[bool, float]] = {}
        for category in self.restricted_concepts:
            blocked, score = await self.check_semantic_similarity(text, category, threshold)
            results[category] = (blocked, score)
        return results

    def get_categories(self) -> tuple[str, ...]:
        """Get all registered category names."""
        return tuple(self.restricted_concepts.keys())


def get_guardrail_service() -> GuardrailService:
    """Get or create the singleton GuardrailService.

    Thread-safe via double-checked locking pattern.

    Returns:
        The singleton GuardrailService instance
    """
    global _guardrail_service
    if _guardrail_service is None:
        with _GUARDRAIL_LOCK:
            if _guardrail_service is None:
                _guardrail_service = GuardrailService()
    return _guardrail_service


def reset_guardrail_service() -> None:
    """Reset the singleton guardrail service (for testing)."""
    global _guardrail_service
    with _GUARDRAIL_LOCK:
        _guardrail_service = None


def semantic_input_guardrail(
    category: str = "harmful",
    threshold: float = 0.4,
    **kwargs: Unpack[GuardrailKwargs],
) -> Callable[[ToolInputGuardrailData], Awaitable[ToolGuardrailFunctionOutput]]:
    """Create a semantic input guardrail for a specific category.

    The returned guardrail checks if tool input arguments are semantically
    similar to restricted concepts in the specified category.

    Args:
        category: Category to check against (default: "harmful")
        threshold: Similarity threshold (default: 0.4)
        **kwargs: Type-safe additional configuration from GuardrailKwargs

    Returns:
        Async guardrail function

    Example:
        >>> @function_tool
        ... async def execute_code(code: str) -> str: ...
        >>> execute_code.tool_input_guardrails.append(
        ...     semantic_input_guardrail("harmful", 0.5)
        ... )
    """
    # Override with kwargs if provided
    effective_category = kwargs.get("category", category)
    effective_threshold = kwargs.get("threshold", threshold)

    async def guardrail(data: ToolInputGuardrailData) -> ToolGuardrailFunctionOutput:
        service = get_guardrail_service()
        args_str = str(data.context.tool_arguments)

        blocked, score = await service.check_semantic_similarity(args_str, category, threshold)

        if blocked:
            return ToolGuardrailFunctionOutput.reject_content(
                message=(
                    f"Input blocked: content semantically similar to restricted "
                    f"category '{category}' (score: {score:.3f})"
                ),
                output_info={"blocked_category": category, "score": score},
            )

        return ToolGuardrailFunctionOutput.allow({"validated_category": category})

    return guardrail


def semantic_output_guardrail(
    category: str = "pii",
    threshold: float = 0.4,
) -> Callable[[ToolOutputGuardrailData], Awaitable[ToolGuardrailFunctionOutput]]:
    """Create a semantic output guardrail for a specific category.

    The returned guardrail checks if tool output contains content
    semantically similar to restricted concepts (e.g., PII).

    For output guardrails, blocking raises an exception to prevent
    the sensitive data from being returned.

    Args:
        category: Category to check against (default: "pii")
        threshold: Similarity threshold (default: 0.4)

    Returns:
        Async guardrail function

    Example:
        >>> @function_tool
        ... async def query_database(sql: str) -> str: ...
        >>> query_database.tool_output_guardrails.append(
        ...     semantic_output_guardrail("pii", 0.3)
        ... )
    """

    async def guardrail(data: ToolOutputGuardrailData) -> ToolGuardrailFunctionOutput:
        service = get_guardrail_service()
        output_str = str(data.output)

        blocked, score = await service.check_semantic_similarity(output_str, category, threshold)

        if blocked:
            # For output guardrails, raise exception to prevent data leakage
            return ToolGuardrailFunctionOutput.raise_exception(
                output_info={
                    "blocked_category": category,
                    "tool": data.context.tool_name,
                    "score": score,
                }
            )

        return ToolGuardrailFunctionOutput.allow({"validated_category": category})

    return guardrail
