"""
Example MCP server using Streamable HTTP transport.
"""

import random
from collections.abc import Callable
from typing import Any, TypeVar, cast

import requests
from mcp.server.fastmcp import FastMCP

# Create server
mcp: Any = FastMCP("Echo Server")


F = TypeVar("F", bound=Callable[..., Any])


def tool() -> Callable[[F], F]:
    """Typed wrapper for mcp.tool decorator."""
    return cast(Callable[[F], F], mcp.tool())


@tool()
def add(a: int, b: int) -> int:
    """Add two numbers"""
    print(f"[debug-server] add({a}, {b})")
    return a + b


@tool()
def get_secret_word() -> str:
    """Get a secret word."""
    print("[debug-server] get_secret_word()")
    return random.choice(["apple", "banana", "cherry"])


@tool()
def get_current_weather(city: str) -> str:
    """Get the current weather for a city."""
    print(f"[debug-server] get_current_weather({city})")

    endpoint = "https://wttr.in"
    response: Any = requests.get(f"{endpoint}/{city}", timeout=10)
    return str(response.text)


if __name__ == "__main__":
    mcp.run(transport="streamable-http")
