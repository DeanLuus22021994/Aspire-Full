#!/usr/bin/env python3
"""Helper metadata for the GitHub MCP container."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True, slots=True)
class MCPContext:
    """Describe the Docker compose configuration for the MCP service."""

    service_name: str
    compose_file: Path
    project_root: Path


def get_context() -> MCPContext:
    """Return compose metadata for the GitHub MCP service."""
    root = Path(__file__).resolve().parents[1]
    # Point compose file that defines the volumes and nvidia runtime
    return MCPContext(
        service_name="github-mcp",
        compose_file=root / "docker-compose.mcp.yml",
        project_root=root,
    )
