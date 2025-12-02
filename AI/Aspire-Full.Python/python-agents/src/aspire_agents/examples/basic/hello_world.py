"""Simple hello world agent with TensorCore compute.

This module demonstrates a minimal agent using:
- Python 3.15+ async/await patterns
- GPU tensor compute initialization (GPU-ONLY)
- Thread-safe execution for free-threading

GPU-ONLY: This example requires a CUDA GPU. No CPU fallback is supported.
"""

import asyncio

from agents import Agent, Runner
from aspire_agents import ensure_tensor_core_gpu


async def main() -> None:
    """Main entry point for the hello world example.

    Initializes TensorCore GPU and runs a simple agent.
    GPU is required - no CPU fallback.
    """
    # Initialize GPU (required - no fallback)
    info = ensure_tensor_core_gpu()
    print(f"[TensorCore] {info.name} ready (cc {info.compute_capability})")

    agent = Agent(
        name="Assistant",
        instructions="You only respond in haikus.",
    )

    result = await Runner.run(agent, "Tell me about recursion in programming.")
    print(result.final_output)
    # Function calls itself,
    # Looping in smaller pieces,
    # Endless by design.


if __name__ == "__main__":
    asyncio.run(main())
