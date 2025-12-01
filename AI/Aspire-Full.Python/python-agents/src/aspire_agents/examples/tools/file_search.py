"""
This module demonstrates the usage of the FileSearchTool with an Agent.
"""

import asyncio

from agents import Agent, FileSearchTool, Runner, trace
from openai import OpenAI as OpenAIClient


async def main() -> None:
    """
    Main entry point for the file search example.
    """
    vector_store_id: str | None = None

    if vector_store_id is None:
        print("### Preparing vector store:\n")
        # Create a new vector store and index a file
        client = OpenAIClient()
        text = (
            "Arrakis, the desert planet in Frank Herbert's 'Dune,' was inspired by the scarcity "
            "of water as a metaphor for oil and other finite resources."
        )
        file_upload = client.files.create(
            file=("example.txt", text.encode("utf-8")),
            purpose="assistants",
        )
        print(f"File uploaded: {file_upload.to_dict()}")

        vector_store = client.vector_stores.create(name="example-vector-store")
        print(f"Vector store created: {vector_store.to_dict()}")

        indexed = client.vector_stores.files.create_and_poll(
            vector_store_id=vector_store.id,
            file_id=file_upload.id,
        )
        print(f"Stored files in vector store: {indexed.to_dict()}")
        vector_store_id = vector_store.id

    # Create an agent that can search the vector store
    agent = Agent(
        name="FileSearch Agent",
        instructions=(
            "You are a helpful agent. "
            "You answer only based on the information in the vector store."
        ),
        model="gpt-4o",
        tools=[
            FileSearchTool(
                max_num_results=3,
                vector_store_ids=[vector_store_id],
                include_search_results=True,
            )
        ],
    )

    with trace("File search example"):
        result = await Runner.run(
            agent, "Be concise, and tell me 1 sentence about Arrakis I might not know."
        )

        print("\n### Final output:\n")
        print(result.final_output)
        # """
        # Arrakis, the desert planet in Frank Herbert's "Dune," was inspired by the scarcity of water
        # as a metaphor for oil and other finite resources.
        # """

        print("\n### Output items:\n")
        print("\n".join([str(out.raw_item) + "\n" for out in result.new_items]))
        # """
        # {"id":"...", "queries":["Arrakis"], "results":[...]}
        # """


if __name__ == "__main__":
    asyncio.run(main())
