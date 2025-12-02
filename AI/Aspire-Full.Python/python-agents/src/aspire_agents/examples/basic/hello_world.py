"""Simple hello world agent with TensorCore compute.

This module demonstrates a minimal agent using:
- Python 3.15+ async/await patterns
- GPU tensor compute initialization
- Thread-safe execution for free-threading

Environment Variables:
- ASPIRE_COMPUTE_MODE: Compute mode - gpu|cpu|hybrid (default: gpu)
"""

import asyncio

from agents import Agent, Runner
from aspire_agents import ASPIRE_COMPUTE_MODE, ensure_tensor_core_gpu


async def main() -> None:
    """Main entry point for the hello world example.

    Initializes TensorCore GPU and runs a simple agent.
    """
    # Initialize GPU if available
    if ASPIRE_COMPUTE_MODE in ("gpu", "hybrid"):
        try:
            info = ensure_tensor_core_gpu()
            print(f"[TensorCore] {info.name} ready")
        except Exception as e:
            print(f"[TensorCore] GPU unavailable: {e}")

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
