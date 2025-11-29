import asyncio

from agents import Agent, Runner
from aspire_agents.gpu import ensure_tensor_core_gpu

URL = "https://upload.wikimedia.org/wikipedia/commons/0/0c/GoldenGateBridge-001.jpg"


async def main():
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
