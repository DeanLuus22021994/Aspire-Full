"""
This module demonstrates the usage of the WebSearchTool with an Agent.
"""

import asyncio

from agents import Agent, Runner, WebSearchTool, trace  # type: ignore


async def main() -> None:
    """
    Main entry point for the web search example.
    """
    agent = Agent(
        name="Web searcher",
        instructions="You are a helpful agent.",
        tools=[WebSearchTool(user_location={"type": "approximate", "city": "New York"})],
    )

    with trace("Web search example"):
        result = await Runner.run(
            agent,
            ("search the web for 'local sports news' and give me 1 interesting update in a " "sentence."),
        )
        print(result.final_output)
        # The New York Giants are reportedly pursuing quarterback Aaron Rodgers after his ...


if __name__ == "__main__":
    asyncio.run(main())
