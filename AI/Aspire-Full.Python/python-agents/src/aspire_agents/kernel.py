"""Semantic Kernel builder helpers for Python 3.15+ free-threaded runtime.

Provides thread-safe Semantic Kernel initialization with support for:
- OpenAI and Azure OpenAI providers
- GitHub-hosted models
- Anthropic models
- Local model deployments

Thread Safety:
- All kernel instances are independent (no shared mutable state)
- Environment variable access is thread-safe
- Lazy imports prevent import-time side effects
"""

from __future__ import annotations

import importlib
import logging
import os
from functools import lru_cache
from typing import TYPE_CHECKING, Any, Final

if TYPE_CHECKING:
    from .config import AgentConfig

logger: Final[logging.Logger] = logging.getLogger(__name__)


class SemanticKernelUnavailableError(RuntimeError):
    """Raised when Semantic Kernel is not installed."""


@lru_cache(maxsize=1)
def _load_semantic_kernel() -> tuple[Any, Any, Any]:
    """Import Semantic Kernel lazily with caching.

    Thread-safe: lru_cache ensures single initialization even with GIL disabled.

    Returns:
        Tuple of (Kernel class, AzureOpenAIChatCompletion, OpenAIChatCompletion)

    Raises:
        SemanticKernelUnavailableError: If semantic-kernel is not installed
    """
    try:
        sk_module = importlib.import_module("semantic_kernel")
        connectors = importlib.import_module("semantic_kernel.connectors.ai.open_ai")
    except ImportError as exc:
        raise SemanticKernelUnavailableError(
            "semantic-kernel is not installed. Run `uv pip install aspire-agents` "
            "or `uv pip install .` from python-agents/."
        ) from exc

    return (
        sk_module.Kernel,
        connectors.AzureChatCompletion,
        connectors.OpenAIChatCompletion,
    )


def _require_env(name: str) -> str:
    """Fetch a required environment variable.

    Thread-safe: os.getenv is thread-safe in Python.

    Args:
        name: Environment variable name

    Returns:
        The environment variable value

    Raises:
        OSError: If the environment variable is not set
    """
    value = os.getenv(name)
    if not value:
        raise OSError(f"Environment variable '{name}' is required for this provider")
    return value


def _get_env_or_none(name: str) -> str | None:
    """Get environment variable or None if not set."""
    return os.getenv(name) or None


def build_kernel(agent: AgentConfig) -> Any:
    """Configure a Semantic Kernel instance for the configured provider.

    Thread-safe: creates independent kernel instances with no shared state.

    Args:
        agent: Agent configuration with model and provider settings

    Returns:
        Configured Semantic Kernel instance

    Raises:
        SemanticKernelUnavailableError: If semantic-kernel is not installed
        OSError: If required environment variables are missing
        ValueError: If provider is not supported

    Examples:
        >>> config = AgentConfig(model=ModelConfig(provider="openai", name="gpt-4o"))
        >>> kernel = build_kernel(config)
    """
    kernel_cls, azure_chat_completion, openai_chat_completion = _load_semantic_kernel()
    kernel = kernel_cls()
    provider = agent.model.provider

    logger.debug(
        "Building Semantic Kernel for provider=%s, model=%s",
        provider,
        agent.model.name,
    )

    if provider == "azure":
        endpoint = agent.model.endpoint or _get_env_or_none("AZURE_OPENAI_ENDPOINT")
        api_key = _require_env("AZURE_OPENAI_API_KEY")
        deployment = agent.model.deployment or agent.model.name
        api_version = agent.model.api_version or "2024-02-01"

        if not endpoint:
            raise OSError("Azure endpoint required: set AZURE_OPENAI_ENDPOINT or provide endpoint in config")

        kernel.add_service(
            azure_chat_completion(
                service_id="azure-default",
                api_key=api_key,
                deployment_name=deployment,
                endpoint=endpoint,
                api_version=api_version,
            )
        )
        logger.info(
            "Semantic Kernel configured with Azure OpenAI: deployment=%s",
            deployment,
        )
        return kernel

    if provider == "github":
        # GitHub-hosted models use OpenAI SDK with custom endpoint
        api_key = _require_env("GITHUB_TOKEN")
        endpoint = agent.model.endpoint or "https://models.inference.ai.azure.com"

        kernel.add_service(
            openai_chat_completion(
                service_id="github-default",
                ai_model_id=agent.model.name,
                api_key=api_key,
                org_id=None,
                default_headers={"X-GitHub-Api-Version": "2022-11-28"},
            )
        )
        logger.info(
            "Semantic Kernel configured with GitHub Models: model=%s",
            agent.model.name,
        )
        return kernel

    if provider in ("openai", "local"):
        api_key = _require_env("OPENAI_API_KEY")
        base_url = _get_env_or_none("OPENAI_BASE_URL")

        # For local provider, require base_url
        if provider == "local" and not base_url:
            base_url = agent.model.endpoint
            if not base_url:
                raise OSError("Local provider requires OPENAI_BASE_URL or endpoint in config")

        kernel.add_service(
            openai_chat_completion(
                service_id=f"{provider}-default",
                ai_model_id=agent.model.name,
                api_key=api_key,
                org_id=_get_env_or_none("OPENAI_ORG_ID"),
            )
        )
        logger.info(
            "Semantic Kernel configured with %s: model=%s",
            provider.upper(),
            agent.model.name,
        )
        return kernel

    if provider == "anthropic":
        # Anthropic requires separate connector (future support)
        raise ValueError(
            f"Provider '{provider}' is not yet supported in Semantic Kernel. " "Use the direct Anthropic SDK instead."
        )

    raise ValueError(f"Unknown provider: {provider}. " f"Supported: openai, azure, github, local")


def build_kernel_with_plugins(
    agent: AgentConfig,
    plugins: dict[str, Any] | None = None,
) -> Any:
    """Build kernel with optional plugin registration.

    Args:
        agent: Agent configuration
        plugins: Optional dict mapping plugin names to plugin objects

    Returns:
        Configured kernel with plugins registered
    """
    kernel = build_kernel(agent)

    if plugins:
        for name, plugin in plugins.items():
            kernel.add_plugin(plugin, plugin_name=name)
            logger.debug("Registered plugin: %s", name)

    return kernel
