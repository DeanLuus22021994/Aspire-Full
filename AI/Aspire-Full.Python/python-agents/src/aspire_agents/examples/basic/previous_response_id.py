"""
This module demonstrates using the previous_response_id to continue a conversation.
"""

import asyncio
from typing import Any

from agents import Agent, Runner

try:
    from aspire_agents.gpu import ensure_tensor_core_gpu
except ImportError:

    def ensure_tensor_core_gpu() -> Any:  # type: ignore
        """Ensure that the tensor core GPU is available."""

# This demonstrates usage of the `previous_response_id` parameter to continue a conversation.
# The second run passes the previous response ID to the model, which allows it to continue the
# conversation without re-sending the previous messages.
#
# Notes:
# 1. This only applies to the OpenAI Responses API. Other models will ignore this parameter.
# 2. Responses are only stored for 30 days as of this writing, so in production you should
# store the response ID along with an expiration date; if the response is no longer valid,
# you'll need to re-send the previous conversation history.


async def main() -> None:
    """
    Main entry point for the previous response ID example.
    """
    ensure_tensor_core_gpu()
    agent = Agent(
        name="Assistant",
        instructions="You are a helpful assistant. be VERY concise.",
    )

    result = await Runner.run(agent, "What is the largest country in South America?")
    print(result.final_output)
    # Brazil

    result = await Runner.run(
        agent,
        "What is the capital of that country?",
        previous_response_id=result.last_response_id,
    )
    print(result.final_output)
    # Brasilia


async def main_stream() -> None:
    """
    Main entry point for the previous response ID example (streaming).
    """
    ensure_tensor_core_gpu()
    agent = Agent(
        name="Assistant",
        instructions="You are a helpful assistant. be VERY concise.",
    )

    result = Runner.run_streamed(agent, "What is the largest country in South America?")

    async for event in result.stream_events():
        if (
            event.type == "raw_response_event"
            and event.data.type == "response.output_text.delta"
        ):
            print(event.data.delta, end="", flush=True)

    print()

    result = Runner.run_streamed(
        agent,
        "What is the capital of that country?",
        previous_response_id=result.last_response_id,
    )

    async for event in result.stream_events():
        if (
            event.type == "raw_response_event"
            and event.data.type == "response.output_text.delta"
        ):
            print(event.data.delta, end="", flush=True)


if __name__ == "__main__":
    is_stream = input("Run in stream mode? (y/n): ")
    if is_stream == "y":
        asyncio.run(main_stream())
    else:
        asyncio.run(main())
