#!/usr/bin/env python3
"""Docker validation for the eamodio.gitlens extension."""

from __future__ import annotations

from pathlib import Path

EXTENSION_ID = "eamodio.gitlens"


def _dockerfile_path() -> Path:
    return Path(__file__).resolve().parents[2] / "Dockerfile"


def test_dockerfile_exists() -> None:
    """Ensure the GitLens Dockerfile is present for image builds."""
    dockerfile = _dockerfile_path()
    assert dockerfile.is_file(), f"Missing Dockerfile for {EXTENSION_ID}"


def test_dockerfile_declares_base_image() -> None:
    """Ensure the Dockerfile contains a FROM directive for base image selection."""
    dockerfile = _dockerfile_path()
    contents = dockerfile.read_text(encoding="utf-8").splitlines()
    # Skip blank/comment lines so the assertion focuses on real instructions.
    meaningful_lines = [
        line.strip()
        for line in contents
        if line.strip() and not line.lstrip().startswith("#")
    ]
    assert meaningful_lines, f"Dockerfile for {EXTENSION_ID} has no instructions"
    assert any(line.upper().startswith("FROM ") for line in meaningful_lines), (
        f"Dockerfile for {EXTENSION_ID} missing FROM instruction"
    )
