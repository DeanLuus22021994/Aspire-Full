"""OpenAI API client abstractions.

Provides protocol definitions for OpenAI API without requiring installation.
"""

from __future__ import annotations

from collections.abc import Sequence
from dataclasses import dataclass
from typing import (
    Any,
    Final,
    Literal,
    Protocol,
    cast,
    runtime_checkable,
)

# ============================================================================
# Message Types
# ============================================================================


@dataclass(frozen=True, slots=True)
class ChatMessage:
    """Chat message structure."""

    role: Literal["system", "user", "assistant", "tool"]
    """Message role."""

    content: str
    """Message content."""

    name: str | None = None
    """Optional name for the message sender."""

    tool_call_id: str | None = None
    """Tool call ID if this is a tool response."""


# ============================================================================
# Chat Completion
# ============================================================================


@dataclass
class ChatCompletionChoice:
    """Single choice in completion response."""

    index: int
    """Choice index."""

    message: ChatMessage
    """Response message."""

    finish_reason: str | None = None
    """Reason for completion."""


@dataclass
class ChatCompletionUsage:
    """Token usage statistics."""

    prompt_tokens: int = 0
    """Tokens in prompt."""

    completion_tokens: int = 0
    """Tokens in completion."""

    total_tokens: int = 0
    """Total tokens used."""


@dataclass
class ChatCompletion:
    """Chat completion response."""

    id: str
    """Completion ID."""

    model: str
    """Model used."""

    choices: list[ChatCompletionChoice]
    """Completion choices."""

    usage: ChatCompletionUsage
    """Token usage."""

    created: int = 0
    """Creation timestamp."""


# ============================================================================
# Embedding Response
# ============================================================================


@dataclass
class EmbeddingData:
    """Single embedding in response."""

    index: int
    """Embedding index."""

    embedding: list[float]
    """Embedding vector."""


@dataclass
class EmbeddingUsage:
    """Embedding token usage."""

    prompt_tokens: int = 0
    """Tokens in prompt."""

    total_tokens: int = 0
    """Total tokens used."""


@dataclass
class EmbeddingResponse:
    """Embedding API response."""

    model: str
    """Model used."""

    data: list[EmbeddingData]
    """Embeddings."""

    usage: EmbeddingUsage
    """Token usage."""


# ============================================================================
# Client Protocol
# ============================================================================


@runtime_checkable
class ChatCompletionsAPI(Protocol):
    """Protocol for chat completions endpoint."""

    async def create(
        self,
        *,
        model: str,
        messages: Sequence[ChatMessage | dict[str, Any]],
        temperature: float = 1.0,
        max_tokens: int | None = None,
        **kwargs: Any,
    ) -> ChatCompletion:
        """Create chat completion."""
        ...


@runtime_checkable
class EmbeddingsAPI(Protocol):
    """Protocol for embeddings endpoint."""

    async def create(
        self,
        *,
        model: str,
        input_text: str | Sequence[str],
        **kwargs: Any,
    ) -> EmbeddingResponse:
        """Create embeddings.

        Args:
            model: Embedding model name
            input_text: Text or texts to embed
            **kwargs: Additional arguments

        Returns:
            EmbeddingResponse with vectors
        """
        ...


@runtime_checkable
class OpenAIClient(Protocol):
    """Protocol for OpenAI client.

    Provides typed interface for OpenAI API.
    """

    @property
    def chat(self) -> ChatCompletionsAPI:
        """Chat completions API."""
        ...

    @property
    def embeddings(self) -> EmbeddingsAPI:
        """Embeddings API."""
        ...


# ============================================================================
# Client Factory
# ============================================================================


def create_openai_client(
    *,
    api_key: str | None = None,
    base_url: str | None = None,
    timeout: float = 60.0,
) -> OpenAIClient:
    """Create OpenAI client.

    Args:
        api_key: API key (uses OPENAI_API_KEY env var if not provided)
        base_url: Base URL for API
        timeout: Request timeout

    Returns:
        OpenAI client instance implementing OpenAIClient protocol.

    Raises:
        RuntimeError: If openai package is not installed.
    """
    try:
        # Dynamic import - module may not be installed
        import importlib

        openai_module: Any = importlib.import_module("openai")
        async_client_class: Any = getattr(openai_module, "AsyncOpenAI")
        client: Any = async_client_class(
            api_key=api_key,
            base_url=base_url,
            timeout=timeout,
        )
        return cast(OpenAIClient, client)
    except ImportError as e:
        msg = "openai package required. Install with: pip install openai"
        raise RuntimeError(msg) from e


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Message types
    "ChatMessage",
    "ChatCompletionChoice",
    "ChatCompletionUsage",
    "ChatCompletion",
    # Embedding types
    "EmbeddingData",
    "EmbeddingUsage",
    "EmbeddingResponse",
    # Client protocols
    "ChatCompletionsAPI",
    "EmbeddingsAPI",
    "OpenAIClient",
    # Factory
    "create_openai_client",
]
