"""
This module demonstrates how to use a remote image as input to an agent.
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


URL = "https://upload.wikimedia.org/wikipedia/commons/0/0c/GoldenGateBridge-001.jpg"


async def main() -> None:
    """
    Main entry point for the remote image example.
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
                "content": [
                    {"type": "input_image", "detail": "auto", "image_url": URL}
                ],
            },
            {
                "role": "user",
                "content": "What do you see in this image?",
            },
        ],
    )
    print(result.final_output)


if __name__ == "__main__":
    asyncio.run(main())
