"""
This module demonstrates how to use a remote PDF file as input to an agent.
"""

import asyncio
from typing import Any

from agents import Agent, Runner

try:
    from aspire_agents.gpu import ensure_tensor_core_gpu
except ImportError:

    def ensure_tensor_core_gpu() -> Any:  # type: ignore
        """Ensure that the tensor core GPU is available."""
        pass


URL = "https://www.berkshirehathaway.com/letters/2024ltr.pdf"


async def main() -> None:
    """
    Main entry point for the remote PDF example.
    """
    ensure_tensor_core_gpu()
    agent = Agent(
        name="Assistant",
        instructions="You are a helpful assistant.",
    )

    result = await Runner.run(
        agent,
        [
            {
                "role": "user",
                "content": [{"type": "input_file", "file_url": URL}],
            },
            {
                "role": "user",
                "content": "Can you summarize the letter?",
            },
        ],
    )
    print(result.final_output)


if __name__ == "__main__":
    asyncio.run(main())
