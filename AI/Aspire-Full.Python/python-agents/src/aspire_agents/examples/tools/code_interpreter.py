"""
This module demonstrates the usage of the CodeInterpreterTool with an Agent.
"""

import asyncio
from typing import Any

from agents import Agent, CodeInterpreterTool, Runner, trace

try:
    from aspire_agents.gpu import ensure_tensor_core_gpu
except ImportError:

    def ensure_tensor_core_gpu() -> Any:  # type: ignore
        pass


async def main() -> None:
    """
    Main entry point for the code interpreter example.
    """
    ensure_tensor_core_gpu()
    agent = Agent(
        name="Code interpreter",
        # Note that using gpt-5 model with streaming for this tool requires org verification
        # Also, code interpreter tool does not support gpt-5's minimal reasoning effort
        model="gpt-4.1",
        instructions="You love doing math.",
        tools=[
            CodeInterpreterTool(
                tool_config={"type": "code_interpreter", "container": {"type": "auto"}},
            )
        ],
    )

    with trace("Code interpreter example"):
        print("Solving math problem...")
        result = Runner.run_streamed(
            agent, "What is the square root of273 * 312821 plus 1782?"
        )
        async for event in result.stream_events():
            if getattr(event, "type", None) == "run_item_stream_event":
                item = getattr(event, "item", None)
                if getattr(item, "type", None) == "tool_call_item":
                    raw_item = getattr(item, "raw_item", None)
                    if getattr(raw_item, "type", None) == "code_interpreter_call":
                        code = getattr(raw_item, "code", None)
                        if code:
                            print(f"Code interpreter code:\n```\n{code}\n```\n")
            elif getattr(event, "type", None) == "run_item_stream_event":
                item = getattr(event, "item", None)
                print(f"Other event: {getattr(item, 'type', 'unknown')}")

        print(f"Final output: {result.final_output}")


if __name__ == "__main__":
    asyncio.run(main())
