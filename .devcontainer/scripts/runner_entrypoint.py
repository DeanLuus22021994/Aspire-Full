#!/usr/bin/env python3
"""Delegates to the canonical implementation in Infra/Aspire-Full.DevContainer/Scripts.

This thin wrapper allows the devcontainer to use the Infra-managed implementation
while maintaining the conventional .devcontainer/scripts/ structure expected by VS Code.
"""

from __future__ import annotations

import sys
from pathlib import Path

# Add Infra scripts to path for import
_WORKSPACE = Path(__file__).resolve().parents[2]
_INFRA_SCRIPTS = _WORKSPACE / "Infra" / "Aspire-Full.DevContainer" / "Scripts"
if str(_INFRA_SCRIPTS) not in sys.path:
    sys.path.insert(0, str(_INFRA_SCRIPTS))

from runner_entrypoint import main  # noqa: E402

if __name__ == "__main__":
    main()
