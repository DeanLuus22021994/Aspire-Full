#!/usr/bin/env python3
"""Handler for GitHub.copilot extension downloads."""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

from helper import get_context


def _fetcher() -> Path:
    return Path(__file__).resolve().parents[1] / "fetch_extension.py"


def main(argv: list[str] | None = None) -> None:
    _ = argv or sys.argv[1:]
    context = get_context()
    env = os.environ.copy()
    env.setdefault("PYTHON_GIL", "0")
    env["EXTENSION_ID"] = context.extension_id
    env["EXTENSION_CACHE"] = str(context.cache_dir)
    subprocess.run(
        ["python3", str(_fetcher())],
        check=True,
        env=env,
    )


if __name__ == "__main__":
    main()
