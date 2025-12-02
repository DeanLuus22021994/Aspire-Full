#!/usr/bin/env python3
"""Docker validation for the ms-dotnettools.csdevkit extension."""

from __future__ import annotations

from pathlib import Path

EXTENSION_ID = "ms-dotnettools.csdevkit"


def _dockerfile_path() -> Path:
    return Path(__file__).resolve().parents[2] / "Dockerfile"


def test_dockerfile_exists() -> None:
    """Ensure the CS Dev Kit Dockerfile is present for builds."""
    dockerfile = _dockerfile_path()
    assert dockerfile.is_file(), f"Missing Dockerfile for {EXTENSION_ID}"


def test_dockerfile_declares_base_image() -> None:
    """Ensure the Dockerfile declares a base image for container builds."""
    dockerfile = _dockerfile_path()
    contents = dockerfile.read_text(encoding="utf-8").splitlines()
    meaningful_lines = [
        line.strip()
        for line in contents
        if line.strip() and not line.lstrip().startswith("#")
    ]
    assert meaningful_lines, f"Dockerfile for {EXTENSION_ID} has no instructions"
    assert any(line.upper().startswith("FROM ") for line in meaningful_lines), (
        f"Dockerfile for {EXTENSION_ID} missing FROM instruction"
    )
