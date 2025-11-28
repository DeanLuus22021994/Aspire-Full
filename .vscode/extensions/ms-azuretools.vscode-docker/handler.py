#!/usr/bin/env python3
"""Handler for ms-azuretools.vscode-docker extension downloads."""

from __future__ import annotations

import os
import subprocess
import sys

from helper import get_context


def main(argv: list[str] | None = None) -> None:
    """Download the Azure Docker VSIX into the cache."""
    _ = argv or sys.argv[1:]
    context = get_context()
    env = os.environ.copy()
    env.setdefault("PYTHON_GIL", "0")
    env["EXTENSION_ID"] = context.extension_id
    env["EXTENSION_CACHE"] = str(context.cache_dir)
    subprocess.run(
        [sys.executable, str(context.fetcher)],
        check=True,
        env=env,
        cwd=context.extension_dir,
    )


if __name__ == "__main__":
    main()
