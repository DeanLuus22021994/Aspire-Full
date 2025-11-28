"""Pytest configuration for python-agents tests."""

from __future__ import annotations

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SRC_ROOT = REPO_ROOT / "python-agents" / "src"

if str(SRC_ROOT) not in sys.path:
    sys.path.insert(0, str(SRC_ROOT))


def pytest_addoption(parser):  # pragma: no cover - pytest hook
    """Register ini options that pytest-asyncio would normally add."""

    parser.addini(
        "asyncio_mode",
        "Pytest-asyncio compatibility shim for local development",
        default="auto",
    )
