"""Semantic Kernel builder helpers."""

from __future__ import annotations

import importlib
import os
from typing import Any, Tuple

from .config import AgentConfig


def _load_semantic_kernel() -> Tuple[Any, Any, Any]:
    """Import Semantic Kernel lazily so dev machines without the package do not lint-fail."""

    try:
        sk_module = importlib.import_module("semantic_kernel")
        connectors = importlib.import_module("semantic_kernel.connectors.ai.open_ai")
    except ImportError as exc:  # pragma: no cover - guidance for missing deps
        raise RuntimeError(
            "semantic-kernel is not installed. Run `uv pip install aspire-agents` "
            "or `uv pip install .` from python-agents/."
        ) from exc
    return (
        sk_module.Kernel,
        connectors.AzureOpenAIChatCompletion,
        connectors.OpenAIChatCompletion,
    )


def _require_env(name: str) -> str:
    """Fetch a required environment variable."""

    value = os.getenv(name)
    if not value:
        raise EnvironmentError(
            f"Environment variable '{name}' is required for this provider"
        )
    return value


def build_kernel(agent: AgentConfig) -> Any:
    """Configure a Semantic Kernel instance for the configured provider."""

    kernel_cls, azure_openai_chat_completion, openai_chat_completion = (
        _load_semantic_kernel()
    )
    kernel = kernel_cls()
    provider = agent.model.provider

    if provider == "azure":
        endpoint = agent.model.endpoint or os.getenv("AZURE_OPENAI_ENDPOINT")
        api_key = _require_env("AZURE_OPENAI_API_KEY")
        deployment = agent.model.deployment or agent.model.name
        kernel.add_service(
            azure_openai_chat_completion(
                service_id="azure-default",
                api_key=api_key,
                deployment_name=deployment,
                endpoint=endpoint,
            )
        )
        return kernel

    api_key = _require_env("OPENAI_API_KEY")
    base_url = os.getenv("OPENAI_BASE_URL")
    kernel.add_service(
        openai_chat_completion(
            service_id="openai-default",
            ai_model_id=agent.model.name,
            api_key=api_key,
            base_url=base_url,
        )
    )
    return kernel
