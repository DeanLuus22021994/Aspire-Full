"""
This module demonstrates streaming text response from an agent.
"""

import asyncio

from agents import Agent, Runner
from aspire_agents.gpu import ensure_tensor_core_gpu
from openai.types.responses import ResponseTextDeltaEvent


async def main() -> None:
    """
    Main entry point for the stream text example.
    """
    ensure_tensor_core_gpu()
    agent = Agent(
        name="Joker",
        instructions="You are a helpful assistant.",
    )

    result = Runner.run_streamed(agent, input="Please tell me 5 jokes.")
    async for event in result.stream_events():
        if event.type == "raw_response_event" and isinstance(
            event.data, ResponseTextDeltaEvent
        ):
            print(event.data.delta, end="", flush=True)


if __name__ == "__main__":
    asyncio.run(main())
