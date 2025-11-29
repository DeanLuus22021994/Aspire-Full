import asyncio

from aspire_agents.gpu import ensure_tensor_core_gpu

from .manager import ResearchManager


async def main() -> None:
    ensure_tensor_core_gpu()
    query = input("What would you like to research? ")
    await ResearchManager().run(query)


if __name__ == "__main__":
    asyncio.run(main())
