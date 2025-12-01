"""
PostgreSQL Session Example for Containerized Development

This example demonstrates using SQLAlchemySession with the PostgreSQL
container from docker-compose, showing production-ready patterns.
"""

import asyncio
import os
from typing import Any, cast

from agents import Agent, Runner
from agents.extensions.memory.sqlalchemy_session import SQLAlchemySession


async def main():
    # Get connection details from environment (set in docker-compose.yml)
    pg_user = os.getenv("POSTGRES_USER", "postgres")
    pg_password = os.getenv("POSTGRES_PASSWORD", "postgres")
    pg_host = os.getenv("POSTGRES_HOST", "postgres")  # Container network name
    pg_port = os.getenv("POSTGRES_PORT", "5432")
    pg_db = "agents"  # Use dedicated agents database

    # Construct PostgreSQL connection URL for asyncpg driver
    database_url = (
        f"postgresql+asyncpg://{pg_user}:{pg_password}@{pg_host}:{pg_port}/{pg_db}"
    )

    print("=== PostgreSQL Session Example ===")
    print(f"Connecting to: {database_url.replace(pg_password, '***')}\n")

    # Create agent
    agent = Agent(
        name="PostgreSQL Assistant",
        instructions="You are a helpful assistant with persistent memory using PostgreSQL.",
    )

    # Create session with connection pooling
    session = SQLAlchemySession.from_url(
        session_id="postgres_user_001",
        url=database_url,
        create_tables=True,  # Auto-create tables on first run
        engine_kwargs={
            "pool_size": 10,  # Connection pool size
            "max_overflow": 20,  # Additional connections beyond pool_size
            "pool_pre_ping": True,  # Verify connections before using
            "pool_recycle": 3600,  # Recycle connections after 1 hour
            "echo": False,  # Set to True for SQL query logging
        },
    )

    try:
        # First conversation turn
        print("Turn 1:")
        print("User: Remember that my favorite color is blue.")
        result = await Runner.run(
            agent,
            "Remember that my favorite color is blue.",
            session=session,
        )
        print(f"Assistant: {result.final_output}\n")

        # Second turn - test memory recall
        print("Turn 2:")
        print("User: What's my favorite color?")
        result = await Runner.run(
            agent,
            "What's my favorite color?",
            session=session,
        )
        print(f"Assistant: {result.final_output}\n")

        # Show session stats
        all_items = await session.get_items()
        print(f"Session contains {len(all_items)} items stored in PostgreSQL")

        # Demonstrate session persistence across "app restarts"
        print("\n=== Simulating App Restart ===")
        print("Creating new session instance with same session_id...\n")

        # Create new session instance (simulates app restart)
        new_session = SQLAlchemySession.from_url(
            session_id="postgres_user_001",  # Same ID
            url=database_url,
            create_tables=False,  # Tables already exist
        )

        # Verify data persisted
        restored_items = await new_session.get_items()
        print(f"Restored {len(restored_items)} items from PostgreSQL")
        print("Memory persisted successfully!\n")

        # Continue conversation with restored session
        print("Turn 3 (after 'restart'):")
        print("User: What did we talk about before?")
        result = await Runner.run(
            agent,
            "What did we talk about before?",
            session=new_session,
        )
        print(f"Assistant: {result.final_output}\n")

        print("=== PostgreSQL Session Example Complete ===")
        print("\nKey Benefits:")
        print("✓ Persistent storage across application restarts")
        print("✓ Production-ready with connection pooling")
        print("✓ Scales horizontally (multiple app instances)")
        print("✓ ACID compliance for data integrity")
        print("✓ Native JSONB support for efficient storage")

    finally:
        # Clean up connection pool
        session_any = cast(Any, session)
        await session_any.engine.dispose()
        print("\nConnection pool closed.")


if __name__ == "__main__":
    # To run this example:
    # 1. Ensure docker-compose services are running: docker-compose up -d
    # 2. Install dependencies: docker-compose exec app-dev pip install "agents[sqlalchemy]"
    # 3. Run: docker-compose exec app-dev python examples/memory/postgres_session_example.py
    asyncio.run(main())
