"""
This module defines the main entry point for the research bot.
"""

import asyncio

from aspire_agents.gpu import ensure_tensor_core_gpu

from .manager import ResearchManager


async def main() -> None:
    """
    Main entry point for the research bot.
    """
    ensure_tensor_core_gpu()
    query = input("What would you like to research? ")
    await ResearchManager().run(query)


if __name__ == "__main__":
    asyncio.run(main())
