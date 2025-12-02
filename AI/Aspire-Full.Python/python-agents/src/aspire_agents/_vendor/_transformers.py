"""HuggingFace Transformers abstractions.

Provides protocol definitions for transformers models and tokenizers
without requiring the transformers package installation.
"""

from __future__ import annotations

from typing import (
    Any,
    Final,
    Protocol,
    Self,
    cast,
    runtime_checkable,
)

from ._torch import Tensor, TorchDevice, TorchModule

# ============================================================================
# Tokenizer Protocol
# ============================================================================


@runtime_checkable
class PreTrainedTokenizer(Protocol):
    """Protocol for HuggingFace tokenizers.

    Defines the interface for text tokenization.
    """

    @property
    def vocab_size(self) -> int:
        """Vocabulary size."""
        ...

    @property
    def model_max_length(self) -> int:
        """Maximum sequence length."""
        ...

    @property
    def pad_token_id(self) -> int | None:
        """Padding token ID."""
        ...

    @property
    def eos_token_id(self) -> int | None:
        """End of sequence token ID."""
        ...

    @property
    def bos_token_id(self) -> int | None:
        """Beginning of sequence token ID."""
        ...

    def __call__(
        self,
        text: str | list[str],
        *,
        padding: bool | str = True,
        truncation: bool | str = True,
        max_length: int | None = None,
        return_tensors: str | None = "pt",
        **kwargs: Any,
    ) -> "BatchEncoding":
        """Tokenize text.

        Args:
            text: Text or list of texts to tokenize
            padding: Whether to pad sequences
            truncation: Whether to truncate sequences
            max_length: Maximum sequence length
            return_tensors: Return type ("pt" for PyTorch)
            **kwargs: Additional arguments

        Returns:
            Tokenized batch
        """
        ...

    def encode(
        self,
        text: str,
        *,
        add_special_tokens: bool = True,
        **kwargs: Any,
    ) -> list[int]:
        """Encode text to token IDs."""
        ...

    def decode(
        self,
        token_ids: list[int] | Tensor,
        *,
        skip_special_tokens: bool = True,
        **kwargs: Any,
    ) -> str:
        """Decode token IDs to text."""
        ...

    def batch_decode(
        self,
        sequences: list[list[int]] | Tensor,
        *,
        skip_special_tokens: bool = True,
        **kwargs: Any,
    ) -> list[str]:
        """Decode batch of token IDs to texts."""
        ...


# ============================================================================
# Batch Encoding
# ============================================================================


@runtime_checkable
class BatchEncoding(Protocol):
    """Protocol for tokenizer output."""

    @property
    def input_ids(self) -> Tensor:
        """Token IDs tensor."""
        ...

    @property
    def attention_mask(self) -> Tensor:
        """Attention mask tensor."""
        ...

    def to(self, device: TorchDevice | str) -> Self:
        """Move to device."""
        ...

    def __getitem__(self, key: str) -> Tensor:
        """Get tensor by key."""
        ...


# ============================================================================
# Model Protocol
# ============================================================================


@runtime_checkable
class PreTrainedModel(TorchModule, Protocol):
    """Protocol for HuggingFace pretrained models.

    Extends TorchModule with generation capabilities.
    """

    @property
    def config(self) -> Any:
        """Model configuration."""
        ...

    @property
    def device(self) -> TorchDevice:
        """Current device."""
        ...

    def generate(
        self,
        input_ids: Tensor,
        *,
        max_new_tokens: int = 100,
        temperature: float = 1.0,
        do_sample: bool = False,
        top_p: float = 1.0,
        top_k: int = 50,
        **kwargs: Any,
    ) -> Tensor:
        """Generate text.

        Args:
            input_ids: Input token IDs
            max_new_tokens: Maximum new tokens to generate
            temperature: Sampling temperature
            do_sample: Whether to use sampling
            top_p: Top-p sampling parameter
            top_k: Top-k sampling parameter
            **kwargs: Additional arguments

        Returns:
            Generated token IDs
        """
        ...


@runtime_checkable
class ModelOutput(Protocol):
    """Protocol for model output."""

    @property
    def last_hidden_state(self) -> Tensor:
        """Last hidden state tensor."""
        ...

    @property
    def hidden_states(self) -> tuple[Tensor, ...] | None:
        """All hidden states if output_hidden_states=True."""
        ...

    @property
    def attentions(self) -> tuple[Tensor, ...] | None:
        """All attention weights if output_attentions=True."""
        ...


# ============================================================================
# Auto Classes (factories)
# ============================================================================


class AutoTokenizer:
    """Factory for loading pretrained tokenizers."""

    @staticmethod
    def from_pretrained(
        model_name_or_path: str,
        *,
        use_fast: bool = True,
        trust_remote_code: bool = False,
        **kwargs: Any,
    ) -> PreTrainedTokenizer:
        """Load pretrained tokenizer.

        Args:
            model_name_or_path: Model identifier or path
            use_fast: Use fast tokenizer implementation
            trust_remote_code: Allow remote code execution
            **kwargs: Additional arguments

        Returns:
            Loaded tokenizer implementing PreTrainedTokenizer protocol.

        Raises:
            RuntimeError: If transformers package is not installed.
        """
        try:
            from transformers import AutoTokenizer as _AutoTokenizer

            tokenizer = _AutoTokenizer.from_pretrained(
                model_name_or_path,
                use_fast=use_fast,
                trust_remote_code=trust_remote_code,
                **kwargs,
            )
            return cast(PreTrainedTokenizer, tokenizer)
        except ImportError as e:
            msg = "transformers package required. Install with: pip install transformers"
            raise RuntimeError(msg) from e


class AutoModel:
    """Factory for loading pretrained models."""

    @staticmethod
    def from_pretrained(
        model_name_or_path: str,
        *,
        trust_remote_code: bool = False,
        torch_dtype: Any = None,
        device_map: str | dict[str, Any] | None = None,
        **kwargs: Any,
    ) -> PreTrainedModel:
        """Load pretrained model.

        Args:
            model_name_or_path: Model identifier or path
            trust_remote_code: Allow remote code execution
            torch_dtype: Data type for model weights
            device_map: Device placement strategy
            **kwargs: Additional arguments

        Returns:
            Loaded model implementing PreTrainedModel protocol.

        Raises:
            RuntimeError: If transformers package is not installed.
        """
        try:
            from transformers import AutoModel as _AutoModel

            model = _AutoModel.from_pretrained(
                model_name_or_path,
                trust_remote_code=trust_remote_code,
                torch_dtype=torch_dtype,
                device_map=device_map,
                **kwargs,
            )
            return cast(PreTrainedModel, model)
        except ImportError as e:
            msg = "transformers package required. Install with: pip install transformers"
            raise RuntimeError(msg) from e


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    "PreTrainedTokenizer",
    "PreTrainedModel",
    "BatchEncoding",
    "ModelOutput",
    "AutoTokenizer",
    "AutoModel",
]
