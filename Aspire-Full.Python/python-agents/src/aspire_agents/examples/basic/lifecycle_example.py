import asyncio
import random
from typing import Any, Optional, cast

from agents import (  # type: ignore # pylint: disable=import-error
    Agent,
    AgentHooks,
    RunContextWrapper,
    RunHooks,
    Runner,
    Tool,
    Usage,
    function_tool,
)
from agents.items import ModelResponse, TResponseInputItem  # type: ignore # pylint: disable=import-error
from agents.tool_context import ToolContext  # type: ignore # pylint: disable=import-error
from pydantic import BaseModel


class LoggingHooks(AgentHooks[Any]):
    async def on_start(
        self,
        context: RunContextWrapper[Any],
        agent: Agent[Any],
    ) -> None:
        # pylint: disable=unused-argument
        print(f"#### {agent.name} is starting.")

    async def on_end(
        self,
        context: RunContextWrapper[Any],
        agent: Agent[Any],
        output: Any,
    ) -> None:
        # pylint: disable=unused-argument
        print(f"#### {agent.name} produced output: {output}.")


class ExampleHooks(RunHooks):
    def __init__(self):
        self.event_counter = 0

    def _usage_to_str(self, usage: Usage) -> str:
        return (
            f"{usage.requests} requests, {usage.input_tokens} input tokens, "
            f"{usage.output_tokens} output tokens, {usage.total_tokens} total tokens"
        )

    async def on_agent_start(self, context: RunContextWrapper, agent: Agent) -> None:
        # pylint: disable=unused-argument
        self.event_counter += 1
        print(f"### {self.event_counter}: Agent {agent.name} started. " f"Usage: {self._usage_to_str(context.usage)}")

    async def on_llm_start(
        self,
        context: RunContextWrapper,
        agent: Agent,
        system_prompt: Optional[str],
        input_items: list[TResponseInputItem],
    ) -> None:
        # pylint: disable=unused-argument
        self.event_counter += 1
        print(f"### {self.event_counter}: LLM started. Usage: {self._usage_to_str(context.usage)}")

    async def on_llm_end(self, context: RunContextWrapper, agent: Agent, response: ModelResponse) -> None:
        # pylint: disable=unused-argument
        self.event_counter += 1
        print(f"### {self.event_counter}: LLM ended. Usage: {self._usage_to_str(context.usage)}")

    async def on_agent_end(self, context: RunContextWrapper, agent: Agent, output: Any) -> None:
        # pylint: disable=unused-argument
        self.event_counter += 1
        print(
            f"### {self.event_counter}: Agent {agent.name} ended with output {output}. "
            f"Usage: {self._usage_to_str(context.usage)}"
        )

    # Note: The on_tool_start and on_tool_end hooks apply only to local tools.
    # They do not include hosted tools that run on the OpenAI server side,
    # such as WebSearchTool, FileSearchTool, CodeInterpreterTool, HostedMCPTool,
    # or other built-in hosted tools.
    async def on_tool_start(self, context: RunContextWrapper, agent: Agent, tool: Tool) -> None:
        # pylint: disable=unused-argument
        self.event_counter += 1
        # While this type cast is not ideal,
        # we don't plan to change the context arg type in the near future for backwards compatibility.
        tool_context = cast(ToolContext[Any], context)
        print(
            f"### {self.event_counter}: Tool {tool.name} started. "
            f"name={tool_context.tool_name}, "  # type: ignore
            f"call_id={tool_context.tool_call_id}, "
            f"args={tool_context.tool_arguments}. "  # type: ignore
            f"Usage: {self._usage_to_str(tool_context.usage)}"
        )

    async def on_tool_end(self, context: RunContextWrapper, agent: Agent, tool: Tool, result: str) -> None:
        # pylint: disable=unused-argument
        self.event_counter += 1
        # While this type cast is not ideal,
        # we don't plan to change the context arg type in the near future for backwards compatibility.
        tool_context = cast(ToolContext[Any], context)
        print(
            f"### {self.event_counter}: Tool {tool.name} finished. result={result}, "
            f"name={tool_context.tool_name}, "  # type: ignore
            f"call_id={tool_context.tool_call_id}, "
            f"args={tool_context.tool_arguments}. "  # type: ignore
            f"Usage: {self._usage_to_str(tool_context.usage)}"
        )

    async def on_handoff(self, context: RunContextWrapper, from_agent: Agent, to_agent: Agent) -> None:
        # pylint: disable=unused-argument
        self.event_counter += 1
        print(
            f"### {self.event_counter}: Handoff from {from_agent.name} to {to_agent.name}. "
            f"Usage: {self._usage_to_str(context.usage)}"
        )


hooks = ExampleHooks()

###


@function_tool
def random_number(max_val: int) -> int:
    """Generate a random number from 0 to max (inclusive)."""
    return random.randint(0, max_val)


@function_tool
def multiply_by_two(x: int) -> int:
    """Return x times two."""
    return x * 2


class FinalResult(BaseModel):
    number: int


multiply_agent = Agent(
    name="Multiply Agent",
    instructions="Multiply the number by 2 and then return the final result.",
    tools=[multiply_by_two],
    output_type=FinalResult,
    hooks=LoggingHooks(),
)

start_agent = Agent(
    name="Start Agent",
    instructions="Generate a random number. If it's even, stop. If it's odd, hand off to the multiplier agent.",
    tools=[random_number],
    output_type=FinalResult,
    handoffs=[multiply_agent],
    hooks=LoggingHooks(),
)


async def main() -> None:
    user_input = input("Enter a max number: ")
    try:
        max_number = int(user_input)
        await Runner.run(
            start_agent,
            hooks=hooks,
            input=f"Generate a random number between 0 and {max_number}.",
        )
    except ValueError:
        print("Please enter a valid integer.")
        return

    print("Done!")


if __name__ == "__main__":
    asyncio.run(main())
# """
# $ python examples/basic/lifecycle_example.py
# ...
# """
