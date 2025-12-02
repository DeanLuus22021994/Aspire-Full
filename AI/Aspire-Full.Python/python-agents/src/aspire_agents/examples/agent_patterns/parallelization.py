"""Parallelization pattern with TensorCore compute integration.

This module demonstrates parallel agent execution leveraging:
- Python 3.15+ free-threading (PYTHON_GIL=0) for true parallelism
- SubAgentOrchestrator for concurrent sub-agent execution
- ASPIRE_SUBAGENT_MAX_CONCURRENT for concurrency control
- GPU memory sharing via ASPIRE_SUBAGENT_GPU_SHARE

Environment Variables:
- ASPIRE_SUBAGENT_MAX_CONCURRENT: Max concurrent sub-agents (default: 16)
- ASPIRE_AGENT_THREAD_POOL_SIZE: Thread pool size (default: 8)
- ASPIRE_COMPUTE_MODE: Compute mode - gpu|cpu|hybrid (default: gpu)
"""

import asyncio

from agents import Agent, ItemHelpers, Runner, trace
from aspire_agents import (
    ASPIRE_SUBAGENT_MAX_CONCURRENT,
    SubAgentOrchestrator,
    get_orchestrator,
)

# Agent configuration for parallel translation
spanish_agent = Agent(
    name="spanish_agent",
    instructions="You translate the user's message to Spanish",
)

translation_picker = Agent(
    name="translation_picker",
    instructions="You pick the best Spanish translation from the given options.",
)


async def main() -> None:
    """Main entry point for the parallelization example.

    Demonstrates parallel agent execution using:
    1. asyncio.gather for concurrent sub-agent calls
    2. trace context for unified telemetry
    3. SubAgentOrchestrator for managed concurrency
    """
    msg = input("Hi! Enter a message, and we'll translate it to Spanish.\n\n")

    # Ensure the entire workflow is a single trace
    with trace("Parallel translation"):
        # Method 1: Using asyncio.gather directly
        res_1, res_2, res_3 = await asyncio.gather(
            Runner.run(
                spanish_agent,
                msg,
            ),
            Runner.run(
                spanish_agent,
                msg,
            ),
            Runner.run(
                spanish_agent,
                msg,
            ),
        )

        outputs = [ItemHelpers.text_message_outputs(res.new_items) for res in [res_1, res_2, res_3]]

        translations = "\n\n".join(outputs)
        print(f"\n\nTranslations:\n\n{translations}")
        print(f"\n(max_concurrent: {ASPIRE_SUBAGENT_MAX_CONCURRENT})")

        best_translation = await Runner.run(
            translation_picker,
            f"Input: {msg}\n\nTranslations:\n{translations}",
        )

    print("\n\n-----")

    print(f"Best translation: {best_translation.final_output}")


async def main_with_orchestrator() -> None:
    """Alternative parallelization using SubAgentOrchestrator.

    Demonstrates managed concurrency with:
    - Automatic GPU memory sharing
    - Configurable concurrency limits
    - Thread pool management
    """
    msg = input("Hi! Enter a message for orchestrated translation.\n\n")

    # Get the singleton orchestrator
    orchestrator = get_orchestrator()

    # Register agents with orchestrator
    orchestrator.register_agent("translator", spanish_agent)
    orchestrator.register_agent("picker", translation_picker)

    with trace("Orchestrated parallel translation"):
        # Execute 3 translations in parallel with managed concurrency
        results = await orchestrator.execute_parallel(
            [
                ("translator", msg),
                ("translator", msg),
                ("translator", msg),
            ]
        )

        translations = "\n\n".join(r.output for r in results if r.success)
        print(f"\n\nTranslations:\n\n{translations}")

        # Get stats from orchestrator
        stats = orchestrator.get_stats()
        print(f"\nOrchestrator stats: {stats}")

        # Pick best translation
        picker_result = await orchestrator.execute_subagent(
            "picker",
            f"Input: {msg}\n\nTranslations:\n{translations}",
        )

    print("\n\n-----")
    print(f"Best translation: {picker_result.output}")


if __name__ == "__main__":
    # Run the standard example
    asyncio.run(main())

    # Uncomment to run orchestrator example:
    # asyncio.run(main_with_orchestrator())
