"""DevContainer scripts package.

This package contains the canonical implementations for VS Code devcontainer lifecycle hooks.
The .devcontainer/scripts/ folder delegates to these modules.
"""

from __future__ import annotations

__all__ = ["post_create", "post_start", "runner_entrypoint"]
