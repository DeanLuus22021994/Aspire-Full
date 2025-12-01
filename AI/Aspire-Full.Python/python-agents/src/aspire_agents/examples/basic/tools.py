"""
This module demonstrates basic tool usage with an agent.
"""

import asyncio
from typing import Annotated, Any

from agents import Agent, Runner, function_tool

try:
    from aspire_agents.gpu import ensure_tensor_core_gpu
except ImportError:

    def ensure_tensor_core_gpu() -> Any:  # type: ignore
        """Ensure that the tensor core GPU is available."""
        pass


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
    """
    Main entry point for the tools example.
    """
    ensure_tensor_core_gpu()
    result = await Runner.run(agent, input="What's the weather in Tokyo?")
    print(result.final_output)
    # The weather in Tokyo is sunny.


if __name__ == "__main__":
    asyncio.run(main())
