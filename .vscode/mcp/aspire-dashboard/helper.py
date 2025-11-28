#!/usr/bin/env python3
"""Helper metadata for the Aspire Dashboard MCP container."""

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
    root = Path(__file__).resolve().parents[1]
    return MCPContext(
        service_name="aspire-dashboard",
        compose_file=root / "docker-compose.mcp.yml",
        project_root=root,
    )
