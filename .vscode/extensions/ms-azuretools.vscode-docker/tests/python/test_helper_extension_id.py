#!/usr/bin/env python3
"""Helper metadata validation for the ms-azuretools.vscode-docker extension."""

from __future__ import annotations

import importlib.util
import sys
from pathlib import Path
from types import ModuleType

EXTENSION_ID = "ms-azuretools.vscode-docker"


def _load_helper() -> ModuleType:
    helper_path = Path(__file__).resolve().parents[2] / "helper.py"
    spec = importlib.util.spec_from_file_location("azure_docker_helper", helper_path)
    if spec is None or spec.loader is None:
        raise RuntimeError("Unable to load ms-azuretools.vscode-docker helper module")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def test_extension_id_matches_helper_contract() -> None:
    """Ensure helper metadata exposes the expected extension identifier."""
    helper_module = _load_helper()
    context = helper_module.get_context()
    assert context.extension_id == EXTENSION_ID, "Unexpected extension identifier"


def test_context_paths_are_consistent() -> None:
    """Ensure helper paths align with the cache layout contract."""
    helper_module = _load_helper()
    context = helper_module.get_context()
    assert context.vsix_file.parent == context.cache_dir, "VSIX should live within cache dir"
    assert context.vsix_file.suffix == ".vsix", "VSIX artifact must use .vsix extension"
    assert context.cache_dir.name == EXTENSION_ID, "Cache directory should match extension id"
    assert context.extension_dir.is_dir(), "Extension directory should exist on disk"
    assert context.fetcher.name == "fetch_extension.py", "Fetcher script should be named consistently"
    assert context.fetcher.is_file(), "Fetcher script should exist alongside extensions"
