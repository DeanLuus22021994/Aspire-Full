from __future__ import annotations

import asyncio
import os
from dataclasses import dataclass
from typing import Final, override

from agents import (
    Agent,
    Model,
    ModelProvider,
    OpenAIChatCompletionsModel,
    RunConfig,
    Runner,
    function_tool,
    set_tracing_disabled,
)
from openai import AsyncOpenAI

_BASE_URL_ENV: Final[str] = "EXAMPLE_BASE_URL"
_API_KEY_ENV: Final[str] = "EXAMPLE_API_KEY"
_MODEL_ENV: Final[str] = "EXAMPLE_MODEL_NAME"


@dataclass(frozen=True)
class ExampleProviderConfig:
    """Configuration contract for the custom provider example."""

    base_url: str
    api_key: str
    model_name: str

    @classmethod
    def from_env(cls) -> ExampleProviderConfig:
        values = {
            "base_url": (os.getenv(_BASE_URL_ENV) or "").strip(),
            "api_key": (os.getenv(_API_KEY_ENV) or "").strip(),
            "model_name": (os.getenv(_MODEL_ENV) or "").strip(),
        }
        missing = [
            env_name
            for env_name, value in zip((_BASE_URL_ENV, _API_KEY_ENV, _MODEL_ENV), values.values(), strict=True)
            if not value
        ]
        if missing:
            raise RuntimeError("Missing configuration for custom provider example: " + ", ".join(missing))
        return cls(**values)


"""Custom provider demo that mixes Runner-managed calls with manual OpenAI usage."""


def configure_tracing() -> None:
    """Disable tracing when no OpenAI tracing key is configured."""

    set_tracing_disabled(disabled=not bool(os.getenv("OPENAI_API_KEY")))


class CustomModelProvider(ModelProvider):
    """Simple provider that always routes to the supplied AsyncOpenAI client."""

    def __init__(self, *, client: AsyncOpenAI, default_model: str) -> None:
        super().__init__()
        self._client = client
        self._default_model = default_model

    @override
    def get_model(self, model_name: str | None) -> Model:
        chosen_model = (model_name or self._default_model).strip()
        return OpenAIChatCompletionsModel(model=chosen_model, openai_client=self._client)


def build_custom_provider(config: ExampleProviderConfig) -> CustomModelProvider:
    client = AsyncOpenAI(base_url=config.base_url, api_key=config.api_key)
    return CustomModelProvider(client=client, default_model=config.model_name)


@function_tool
def get_weather(city: str):
    print(f"[debug] getting weather for {city}")
    return f"The weather in {city} is sunny."


async def main() -> None:
    configure_tracing()
    config = ExampleProviderConfig.from_env()
    provider = build_custom_provider(config)
    agent = Agent(
        name="Assistant",
        instructions="You only respond in haikus.",
        tools=[get_weather],
    )

    # This will use the custom model provider
    result = await Runner.run(
        agent,
        "What's the weather in Tokyo?",
        run_config=RunConfig(model_provider=provider),
    )
    print(result.final_output)

    # If you uncomment this, it will use OpenAI directly, not the custom provider
    # result = await Runner.run(
    #     agent,
    #     "What's the weather in Tokyo?",
    # )
    # print(result.final_output)


if __name__ == "__main__":
    asyncio.run(main())
