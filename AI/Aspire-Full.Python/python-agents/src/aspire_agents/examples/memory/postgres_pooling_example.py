"""
Example of using PostgreSQL connection pooling with the Agent Framework.

This example demonstrates:
1. Creating a custom session manager that uses SQLAlchemy's connection pooling
2. Handling async database connections efficiently
3. Implementing the SessionManager protocol for PostgreSQL
"""

from __future__ import annotations

import json
import os
from datetime import datetime
from typing import TYPE_CHECKING, Any, Dict, Optional

from aspire_agents.memory.session import SessionManager

# Handle optional dependencies without type ignores
if TYPE_CHECKING:
    from sqlalchemy import text
    from sqlalchemy.ext.asyncio import AsyncEngine, create_async_engine
else:
    try:
        from sqlalchemy import text
        from sqlalchemy.ext.asyncio import AsyncEngine, create_async_engine
    except ImportError:
        text = None
        create_async_engine = None
        AsyncEngine = Any


class PostgreSQLSessionManager(SessionManager):
    """
    A production-ready session manager using PostgreSQL with connection pooling.
    """

    def __init__(self, connection_string: str, pool_size: int = 5) -> None:
        """
        Initialize the session manager.

        Args:
            connection_string: PostgreSQL connection string
            pool_size: Size of the connection pool
        """
        if create_async_engine is None:
            raise ImportError(
                "SQLAlchemy is required for PostgreSQL support. "
                "Install it with: pip install sqlalchemy asyncpg"
            )

        # Use Any for the engine to avoid static analysis issues when library is missing
        # This avoids "Unknown type" errors without using type: ignore
        creator: Any = create_async_engine
        self._engine: Any = creator(
            connection_string,
            pool_size=pool_size,
            max_overflow=10,
            pool_pre_ping=True,  # Verify connections before using them
        )

    async def create_session(
        self, session_id: str, metadata: Optional[Dict[str, Any]] = None
    ) -> None:
        """Create a new session in the database."""
        # Ensure table exists (in production, use migrations instead)
        async with self._engine.begin() as conn:
            await conn.execute(
                text(
                    """
                CREATE TABLE IF NOT EXISTS sessions (
                    session_id VARCHAR(255) PRIMARY KEY,
                    metadata JSONB,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            """
                )
            )

            # Insert session
            await conn.execute(
                text(
                    """
                INSERT INTO sessions (session_id, metadata)
                VALUES (:session_id, :metadata)
                ON CONFLICT (session_id) DO UPDATE
                SET metadata = :metadata, updated_at = CURRENT_TIMESTAMP
            """
                ),
                {"session_id": session_id, "metadata": json.dumps(metadata or {})},
            )

    async def get_session(self, session_id: str) -> Optional[Dict[str, Any]]:
        """Retrieve session metadata."""
        async with self._engine.connect() as conn:
            result = await conn.execute(
                text("SELECT metadata FROM sessions WHERE session_id = :session_id"),
                {"session_id": session_id},
            )
            row = result.fetchone()
            if row:
                return dict(json.loads(row[0]))
            return None

    async def close(self) -> None:
        """Close the connection pool."""
        await self._engine.dispose()


async def main() -> None:
    """Run the PostgreSQL pooling example."""
    # Check if dependencies are available
    if create_async_engine is None:
        print("Skipping example: sqlalchemy not installed")
        return

    # Get connection parameters from environment or use defaults
    postgres_host = os.getenv("POSTGRES_HOST", "localhost")
    postgres_port = os.getenv("POSTGRES_PORT", "5432")
    postgres_user = os.getenv("POSTGRES_USER", "postgres")
    postgres_password = os.getenv("POSTGRES_PASSWORD", "postgres")
    postgres_db = "agents"

    connection_string = (
        f"postgresql+asyncpg://{postgres_user}:{postgres_password}"
        f"@{postgres_host}:{postgres_port}/{postgres_db}"
    )

    print(f"Connecting to PostgreSQL at {postgres_host}:{postgres_port}...")

    # Initialize session manager
    session_manager = PostgreSQLSessionManager(connection_string)

    # Create a session
    session_id = f"user-session-{int(datetime.now().timestamp())}"
    print(f"Creating session: {session_id}")
    await session_manager.create_session(
        session_id, {"user_id": "user123", "role": "admin", "theme": "dark"}
    )

    # Retrieve session
    print("Retrieving session...")
    session_data = await session_manager.get_session(session_id)
    print(f"Session data: {session_data}")

    # Clean up
    await session_manager.close()
    print("Connection pool closed.")


if __name__ == "__main__":
    import asyncio

    asyncio.run(main())
