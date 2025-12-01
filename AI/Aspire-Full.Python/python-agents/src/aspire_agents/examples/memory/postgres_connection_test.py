"""
Test PostgreSQL connection and session storage without requiring API keys.

This example validates:
1. Database connection using environment variables from docker-compose
2. SQLAlchemy async engine creation
3. Connection pool functionality
4. Session table creation and data persistence
"""

import asyncio
import os
from datetime import datetime
from typing import TYPE_CHECKING, Any, cast

if TYPE_CHECKING:
    from sqlalchemy.ext.asyncio import AsyncEngine  # type: ignore
else:
    try:
        from sqlalchemy.ext.asyncio import AsyncEngine
    except ImportError:
        AsyncEngine = Any

try:
    from sqlalchemy import text  # type: ignore
    from sqlalchemy.ext.asyncio import create_async_engine  # type: ignore
except ImportError:
    text = None  # type: ignore
    create_async_engine = None  # type: ignore


async def test_postgres_connection() -> None:
    """Test PostgreSQL connection and basic operations."""
    if create_async_engine is None or text is None:
        print("Skipping test: sqlalchemy not installed")
        return

    # Get connection parameters from environment
    postgres_host = os.getenv("POSTGRES_HOST", "localhost")
    postgres_port = os.getenv("POSTGRES_PORT", "5432")
    postgres_user = os.getenv("POSTGRES_USER", "postgres")
    postgres_password = os.getenv("POSTGRES_PASSWORD", "postgres")
    postgres_db = "agents"  # Use the agents database created by 02-init-sqlalchemy.sql

    # Create connection string
    connection_string = (
        f"postgresql+asyncpg://{postgres_user}:{postgres_password}"
        f"@{postgres_host}:{postgres_port}/{postgres_db}"
    )

    print("=== PostgreSQL Connection Test ===")
    print(f"Host: {postgres_host}:{postgres_port}")
    print(f"Database: {postgres_db}")
    print(f"User: {postgres_user}")
    print()

    # Create async engine with connection pooling
    creator = cast(Any, create_async_engine)
    engine: AsyncEngine = creator(
        connection_string,
        echo=False,  # Set to True to see SQL queries
        pool_size=5,
        max_overflow=10,
        pool_pre_ping=True,  # Verify connections before using them
    )

    try:
        # Test 1: Basic connection
        print("Test 1: Testing database connection...")
        engine_any = cast(Any, engine)
        async with engine_any.connect() as conn:
            result = await conn.execute(text("SELECT version()"))
            version = result.scalar()
            print(f"✓ Connected! PostgreSQL version: {version[:50]}...")
        print()

        # Test 2: Check installed extensions
        print("Test 2: Checking installed extensions...")
        async with engine_any.connect() as conn:
            result = await conn.execute(
                text("SELECT extname, extversion FROM pg_extension ORDER BY extname")
            )
            extensions = result.fetchall()
            for ext in extensions:
                print(f"  • {ext[0]} v{ext[1]}")
        print()

        # Test 3: Create test table and insert data
        print("Test 3: Creating test table and inserting data...")
        async with engine_any.begin() as conn:
            # Create test table
            await conn.execute(
                text(
                    """
                CREATE TABLE IF NOT EXISTS test_sessions (
                    id SERIAL PRIMARY KEY,
                    session_id VARCHAR(255) NOT NULL,
                    data JSONB NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            """
                )
            )

            # Insert test data
            await conn.execute(
                text(
                    """
                INSERT INTO test_sessions (session_id, data)
                VALUES (:session_id, :data)
            """
                ),
                {
                    "session_id": "test-session-1",
                    "data": '{"test": "data", "timestamp": "'
                    + datetime.now().isoformat()
                    + '"}',
                },
            )
            print("✓ Test table created and data inserted")
        print()

        # Test 4: Query the data back
        print("Test 4: Querying data...")
        async with engine_any.connect() as conn:
            result = await conn.execute(text("SELECT * FROM test_sessions"))
            rows = result.fetchall()
            print(f"✓ Found {len(rows)} row(s):")
            for row in rows:
                print(f"  • Session ID: {row[1]}, Data: {row[2]}")
        print()

        # Test 5: Check connection pool status
        print("Test 5: Connection pool status...")
        print(f"  • Pool size: {engine_any.pool.size()}")
        print(f"  • Checked out connections: {engine_any.pool.checkedout()}")
        print(f"  • Overflow: {engine_any.pool.overflow()}")
        print()

        # Test 6: Cleanup - drop test table
        print("Test 6: Cleaning up test table...")
        async with engine_any.begin() as conn:
            await conn.execute(text("DROP TABLE IF EXISTS test_sessions"))
            print("✓ Test table dropped")

        print()
        print("=" * 50)
        print("✓ All tests passed! PostgreSQL is configured correctly.")
        print("=" * 50)

    except Exception as e:
        print(f"✗ Error: {e}")
        raise
    finally:
        # Close the engine
        engine_any = cast(Any, engine)
        await engine_any.dispose()
        print("\nConnection pool closed.")


if __name__ == "__main__":
    asyncio.run(test_postgres_connection())
