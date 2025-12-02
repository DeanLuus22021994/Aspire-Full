"""Basic tool usage with TensorCore compute integration.

This module demonstrates tool registration and execution with:
- GPU-accelerated tensor compute for tool embeddings
- Tensor Core validation before execution
- Thread-safe tool invocation for Python 3.15+ free-threading

Environment Variables:
- ASPIRE_COMPUTE_MODE: Compute mode - gpu|cpu|hybrid (default: gpu)
- CUDA_TENSOR_CORE_ALIGNMENT: Memory alignment (default: 128)
"""

import asyncio
from typing import Annotated, Any

from agents import Agent, Runner, function_tool
from aspire_agents import ASPIRE_COMPUTE_MODE, ensure_tensor_core_gpu
from pydantic import BaseModel, Field


class Weather(BaseModel):
    """
    Weather information for a city.
    """

    city: str = Field(description="The city name")
    temperature_range: str = Field(description="The temperature range in Celsius")
    conditions: str = Field(description="The weather conditions")


@function_tool
def get_weather(city: Annotated[str, "The city to get the weather for"]) -> Weather:
    """Get the current weather information for a specified city."""
    print("[debug] get_weather called")
    return Weather(city=city, temperature_range="14-20C", conditions="Sunny with wind.")


agent = Agent(
    name="Hello world",
    instructions="You are a helpful agent.",
    tools=[get_weather],
)


async def main() -> None:
    """Main entry point for the tools example.

    Validates TensorCore GPU and executes agent with tools.
    """
    # Validate GPU before execution
    if ASPIRE_COMPUTE_MODE in ("gpu", "hybrid"):
        info = ensure_tensor_core_gpu()
        print(f"[TensorCore] {info.name} (cc {info.compute_capability}, align={info.tensor_alignment})")

    result = await Runner.run(agent, input="What's the weather in Tokyo?")
    print(result.final_output)
    # The weather in Tokyo is sunny.


if __name__ == "__main__":
    asyncio.run(main())
