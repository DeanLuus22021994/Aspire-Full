"""
Advanced PostgreSQL Connection Pooling Example

Demonstrates production-ready connection pooling patterns,
health checks, and session management best practices.
"""

from __future__ import annotations

import asyncio
import os
from contextlib import asynccontextmanager
from typing import Any, AsyncIterator

from agents import Agent, Runner
from agents.extensions.memory.sqlalchemy_session import SQLAlchemySession

# Define types for optional dependencies to satisfy static analysis
AsyncEngine: Any
try:
    from sqlalchemy import text
    from sqlalchemy.ext.asyncio import AsyncEngine, create_async_engine
except ImportError:
    text = None
    create_async_engine = None
    AsyncEngine = Any


class PostgreSQLSessionManager:
    """
    Production-ready session manager with connection pooling.

    Features:
    - Singleton engine pattern
    - Connection pool health monitoring
    - Graceful shutdown
    - Environment-based configuration
    """

    def __init__(self) -> None:
        self._engine: Any | None = None
        self._connection_url = self._build_connection_url()

    def _build_connection_url(self) -> str:
        """Build PostgreSQL connection URL from environment variables."""
        user = os.getenv("POSTGRES_USER", "postgres")
        password = os.getenv("POSTGRES_PASSWORD", "postgres")
        host = os.getenv("POSTGRES_HOST", "postgres")
        port = os.getenv("POSTGRES_PORT", "5432")
        database = "agents"  # Use dedicated agents database

        return f"postgresql+asyncpg://{user}:{password}@{host}:{port}/{database}"

    def get_engine(self) -> Any:
        """Get or create the singleton engine instance."""
        if self._engine is None:
            if create_async_engine is None:
                raise ImportError("sqlalchemy.ext.asyncio is required")

            # We use Any for the creator to avoid type errors when sqlalchemy is missing
            creator: Any = create_async_engine
            self._engine = creator(
                self._connection_url,
                # Pool configuration
                pool_size=20,  # Base connection pool size
                max_overflow=10,  # Additional connections under load
                pool_timeout=30,  # Max seconds to wait for connection
                pool_recycle=1800,  # Recycle connections after 30 min
                pool_pre_ping=True,  # Health check before using connection
                # Performance tuning
                echo=False,  # Disable SQL logging in production
                echo_pool=False,  # Disable pool logging
                # Connection arguments for asyncpg
                connect_args={
                    "server_settings": {
                        "application_name": "openai_agents_sdk",
                        "jit": "off",  # Disable JIT for faster query startup
                    },
                    "command_timeout": 60,  # Query timeout in seconds
                    "timeout": 10,  # Connection timeout
                },
            )
        return self._engine

    async def get_pool_status(self) -> dict[str, Any]:
        """Get current connection pool statistics."""
        if self._engine is None:
            return {"status": "not_initialized"}

        # Access pool attributes dynamically to avoid type errors if AsyncEngine is Any
        engine: Any = self._engine
        pool = engine.pool
        return {
            "size": pool.size(),
            "checked_in": pool.checkedin(),
            "checked_out": pool.checkedout(),
            "overflow": pool.overflow(),
            # Access protected members for stats (common pattern in sqlalchemy monitoring)
            "max_overflow": getattr(pool, "_max_overflow", -1),
            "pool_size": getattr(getattr(pool, "_pool", None), "maxsize", -1),
        }

    async def health_check(self) -> bool:
        """Verify database connectivity."""
        try:
            if text is None:
                return False
            engine: Any = self.get_engine()
            async with engine.connect() as conn:
                await conn.execute(text("SELECT 1"))
            return True
        except Exception as e:
            # Catching generic Exception is appropriate for a health check wrapper
            print(f"Health check failed: {e}")
            return False

    async def close(self) -> None:
        """Gracefully close all connections."""
        if self._engine:
            engine: Any = self._engine
            await engine.dispose()
            self._engine = None

    @asynccontextmanager
    async def create_session(self, session_id: str) -> AsyncIterator[SQLAlchemySession]:
        """
        Context manager for creating sessions.

        Usage:
            async with manager.create_session("user_123") as session:
                await Runner.run(agent, "Hello", session=session)
        """
        engine = self.get_engine()
        session = SQLAlchemySession(
            session_id=session_id,
            engine=engine,
            create_tables=True,
        )
        try:
            yield session
        finally:
            pass  # Engine disposal handled by manager.close()


async def main() -> None:
    """Run the advanced PostgreSQL pooling example."""
    print("=== Advanced PostgreSQL Pooling Example ===\n")

    # Initialize session manager
    manager = PostgreSQLSessionManager()

    # Health check
    print("Performing health check...")
    if not await manager.health_check():
        print("ERROR: Database not accessible!")
        return
    print("✓ Database healthy\n")

    # Show initial pool stats
    stats = await manager.get_pool_status()
    print(f"Initial pool stats: {stats}\n")

    # Create agent
    agent = Agent(
        name="Pooling Demo Agent",
        instructions="Be helpful and concise.",
    )

    # Simulate multiple concurrent sessions
    async def run_conversation(user_id: str, message: str):
        """Run a single conversation turn."""
        async with manager.create_session(f"user_{user_id}") as session:
            result = await Runner.run(agent, message, session=session)
            return f"User {user_id}: {result.final_output}"

    print("Running 5 concurrent conversations...")
    tasks = [
        run_conversation("1", "What is Python?"),
        run_conversation("2", "What is Docker?"),
        run_conversation("3", "What is PostgreSQL?"),
        run_conversation("4", "What is SQLAlchemy?"),
        run_conversation("5", "What is async/await?"),
    ]

    results = await asyncio.gather(*tasks)
    for result in results:
        print(f"  {result}")
    print()

    # Show pool stats after load
    stats = await manager.get_pool_status()
    print(f"Pool stats after load: {stats}\n")

    # Cleanup
    print("Closing connection pool...")
    await manager.close()
    print("✓ Pool closed gracefully\n")

    print("=== Example Complete ===")
    print("\nProduction Best Practices Demonstrated:")
    print("✓ Singleton engine pattern")
    print("✓ Connection pooling with overflow")
    print("✓ Health checks and monitoring")
    print("✓ Graceful shutdown")
    print("✓ Concurrent session handling")
    print("✓ Context manager patterns")


if __name__ == "__main__":
    # To run this example:
    # docker-compose exec app-# filepath: c:\Users\Dean\source\Aspire-Full - 1\AI\Aspire-Full.Python\python-agents\src\aspire_agents\examples\memory\postgres_pooling_example.py
"""
Advanced PostgreSQL Connection Pooling Example

Demonstrates production-ready connection pooling patterns,
health checks, and session management best practices.
"""

from __future__ import annotations

import asyncio
import os
from contextlib import asynccontextmanager
from typing import Any, AsyncIterator

from agents import Agent, Runner
from agents.extensions.memory.sqlalchemy_session import SQLAlchemySession

# Define types for optional dependencies to satisfy static analysis
AsyncEngine: Any
try:
    from sqlalchemy import text
    from sqlalchemy.ext.asyncio import AsyncEngine, create_async_engine
except ImportError:
    text = None
    create_async_engine = None
    AsyncEngine = Any


class PostgreSQLSessionManager:
    """
    Production-ready session manager with connection pooling.

    Features:
    - Singleton engine pattern
    - Connection pool health monitoring
    - Graceful shutdown
    - Environment-based configuration
    """

    def __init__(self) -> None:
        self._engine: Any | None = None
        self._connection_url = self._build_connection_url()

    def _build_connection_url(self) -> str:
        """Build PostgreSQL connection URL from environment variables."""
        user = os.getenv("POSTGRES_USER", "postgres")
        password = os.getenv("POSTGRES_PASSWORD", "postgres")
        host = os.getenv("POSTGRES_HOST", "postgres")
        port = os.getenv("POSTGRES_PORT", "5432")
        database = "agents"  # Use dedicated agents database

        return f"postgresql+asyncpg://{user}:{password}@{host}:{port}/{database}"

    def get_engine(self) -> Any:
        """Get or create the singleton engine instance."""
        if self._engine is None:
            if create_async_engine is None:
                raise ImportError("sqlalchemy.ext.asyncio is required")

            # We use Any for the creator to avoid type errors when sqlalchemy is missing
            creator: Any = create_async_engine
            self._engine = creator(
                self._connection_url,
                # Pool configuration
                pool_size=20,  # Base connection pool size
                max_overflow=10,  # Additional connections under load
                pool_timeout=30,  # Max seconds to wait for connection
                pool_recycle=1800,  # Recycle connections after 30 min
                pool_pre_ping=True,  # Health check before using connection
                # Performance tuning
                echo=False,  # Disable SQL logging in production
                echo_pool=False,  # Disable pool logging
                # Connection arguments for asyncpg
                connect_args={
                    "server_settings": {
                        "application_name": "openai_agents_sdk",
                        "jit": "off",  # Disable JIT for faster query startup
                    },
                    "command_timeout": 60,  # Query timeout in seconds
                    "timeout": 10,  # Connection timeout
                },
            )
        return self._engine

    async def get_pool_status(self) -> dict[str, Any]:
        """Get current connection pool statistics."""
        if self._engine is None:
            return {"status": "not_initialized"}

        # Access pool attributes dynamically to avoid type errors if AsyncEngine is Any
        engine: Any = self._engine
        pool = engine.pool
        return {
            "size": pool.size(),
            "checked_in": pool.checkedin(),
            "checked_out": pool.checkedout(),
            "overflow": pool.overflow(),
            # Access protected members for stats (common pattern in sqlalchemy monitoring)
            "max_overflow": getattr(pool, "_max_overflow", -1),
            "pool_size": getattr(getattr(pool, "_pool", None), "maxsize", -1),
        }

    async def health_check(self) -> bool:
        """Verify database connectivity."""
        try:
            if text is None:
                return False
            engine: Any = self.get_engine()
            async with engine.connect() as conn:
                await conn.execute(text("SELECT 1"))
            return True
        except Exception as e:
            # Catching generic Exception is appropriate for a health check wrapper
            print(f"Health check failed: {e}")
            return False

    async def close(self) -> None:
        """Gracefully close all connections."""
        if self._engine:
            engine: Any = self._engine
            await engine.dispose()
            self._engine = None

    @asynccontextmanager
    async def create_session(self, session_id: str) -> AsyncIterator[SQLAlchemySession]:
        """
        Context manager for creating sessions.

        Usage:
            async with manager.create_session("user_123") as session:
                await Runner.run(agent, "Hello", session=session)
        """
        engine = self.get_engine()
        session = SQLAlchemySession(
            session_id=session_id,
            engine=engine,
            create_tables=True,
        )
        try:
            yield session
        finally:
            pass  # Engine disposal handled by manager.close()


async def main() -> None:
    """Run the advanced PostgreSQL pooling example."""
    print("=== Advanced PostgreSQL Pooling Example ===\n")

    # Initialize session manager
    manager = PostgreSQLSessionManager()

    # Health check
    print("Performing health check...")
    if not await manager.health_check():
        print("ERROR: Database not accessible!")
        return
    print("✓ Database healthy\n")

    # Show initial pool stats
    stats = await manager.get_pool_status()
    print(f"Initial pool stats: {stats}\n")

    # Create agent
    agent = Agent(
        name="Pooling Demo Agent",
        instructions="Be helpful and concise.",
    )

    # Simulate multiple concurrent sessions
    async def run_conversation(user_id: str, message: str):
        """Run a single conversation turn."""
        async with manager.create_session(f"user_{user_id}") as session:
            result = await Runner.run(agent, message, session=session)
            return f"User {user_id}: {result.final_output}"

    print("Running 5 concurrent conversations...")
    tasks = [
        run_conversation("1", "What is Python?"),
        run_conversation("2", "What is Docker?"),
        run_conversation("3", "What is PostgreSQL?"),
        run_conversation("4", "What is SQLAlchemy?"),
        run_conversation("5", "What is async/await?"),
    ]

    results = await asyncio.gather(*tasks)
    for result in results:
        print(f"  {result}")
    print()

    # Show pool stats after load
    stats = await manager.get_pool_status()
    print(f"Pool stats after load: {stats}\n")

    # Cleanup
    print("Closing connection pool...")
    await manager.close()
    print("✓ Pool closed gracefully\n")

    print("=== Example Complete ===")
    print("\nProduction Best Practices Demonstrated:")
    print("✓ Singleton engine pattern")
    print("✓ Connection pooling with overflow")
    print("✓ Health checks and monitoring")
    print("✓ Graceful shutdown")
    print("✓ Concurrent session handling")
    print("✓ Context manager patterns")


if __name__ == "__main__":
    # To run this example:
    # docker-compose exec app-
