"""
This module demonstrates how to track usage (tokens, requests) for an Agent.
"""

import asyncio

from agents import Agent, Runner, Usage, function_tool
from pydantic import BaseModel

from aspire_agents.gpu import ensure_tensor_core_gpu


class Weather(BaseModel):
    """
    Weather information for a city.
    """

    city: str
    temperature_range: str
    conditions: str


@function_tool
def get_weather(city: str) -> Weather:
    """Get the current weather information for a specified city."""
    return Weather(city=city, temperature_range="14-20C", conditions="Sunny with wind.")


def print_usage(usage: Usage) -> None:
    """
    Print the usage statistics.
    """
    print("\n=== Usage ===")
    print(f"Input tokens: {usage.input_tokens}")
    print(f"Output tokens: {usage.output_tokens}")
    print(f"Total tokens: {usage.total_tokens}")
    print(f"Requests: {usage.requests}")
    for i, request in enumerate(usage.request_usage_entries):  # type: ignore
        print(f"  {i + 1}: {request.input_tokens} input, {request.output_tokens} output")


async def main() -> None:
    """
    Main entry point for the usage tracking example.
    """
    ensure_tensor_core_gpu()
    agent = Agent(
        name="Usage Demo",
        instructions="You are a concise assistant. Use tools if needed.",
        tools=[get_weather],
    )

    result = await Runner.run(agent, "What's the weather in Tokyo?")

    print("\nFinal output:")
    print(result.final_output)

    # Access usage from the run context
    print_usage(result.context_wrapper.usage)


if __name__ == "__main__":
    asyncio.run(main())
