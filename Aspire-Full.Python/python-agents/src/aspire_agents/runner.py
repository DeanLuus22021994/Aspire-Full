"""High-level runner that wraps Semantic Kernel for Aspire agents."""

from __future__ import annotations

import asyncio
import importlib
from dataclasses import dataclass
from typing import Any

from rich.console import Console

from .config import AgentConfig
from .gpu import TensorCoreInfo, ensure_tensor_core_gpu
from .kernel import build_kernel

console = Console()


def _create_prompt_template(config: AgentConfig) -> Any:
    try:
        sk_prompt_module = importlib.import_module("semantic_kernel.prompt_template")
    except ImportError as exc:  # pragma: no cover
        raise RuntimeError(
            "semantic-kernel is not installed. Install dependencies before running agents."
        ) from exc

    prompt_template = sk_prompt_module.PromptTemplateConfig(
        template="{{$system_prompt}}\n\n{{$input}}",
        name=config.name,
        template_format="semantic-kernel",
        description=config.description,
    )
    prompt_template.input_variables = [
        {"name": "system_prompt", "description": "Agent instructions"},
        {"name": "input", "description": "User supplied request"},
    ]
    return prompt_template


@dataclass(slots=True)
class AgentResult:
    """Returned content plus downstream handoff identifiers."""

    content: str
    handoffs: list[str]


class AgentRunner:
    """Build and execute Semantic Kernel prompts based on a manifest."""

    def __init__(self, config: AgentConfig):
        self.config = config
        self.tensor_info: TensorCoreInfo = ensure_tensor_core_gpu()
        self.kernel = build_kernel(config)
        prompt_template_config = _create_prompt_template(config)

        plugin = config.name.lower().replace(" ", "-")
        self.function = self.kernel.add_function_from_prompt(
            plugin_name=f"{plugin}-plugin",
            function_name="respond",
            prompt_template_config=prompt_template_config,
        )

    async def arun(self, user_input: str) -> AgentResult:
        """Execute the agent asynchronously."""
        response = await self.kernel.invoke(
            self.function,
            system_prompt=self.config.prompt,
            input=user_input,
        )
        return AgentResult(
            content=str(response),
            handoffs=self.config.handoffs,
        )

    def run(self, user_input: str) -> AgentResult:
        """Synchronous helper for consumers that cannot await."""
        return asyncio.run(self.arun(user_input))

    def pretty_print(self, result: AgentResult) -> None:
        """Render agent output and handoffs to the console."""
        console.rule(f"{self.config.name} :: output")
        console.print(result.content)
        if result.handoffs:
            console.rule("handoffs")
            for handoff in result.handoffs:
                console.print(f"- {handoff}")
