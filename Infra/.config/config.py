#!/usr/bin/env python3
"""Shared lint/test configuration loader and helpers."""

from __future__ import annotations

from collections.abc import Sequence
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Any

try:
    import yaml  # type: ignore
except ImportError as exc:
    raise RuntimeError("PyYAML is required to load config.yaml") from exc

REPO_ROOT = Path(__file__).resolve().parents[1]
CONFIG_PATH = REPO_ROOT / ".config" / "config.yaml"


def _as_tuple(values: Sequence[str] | None) -> tuple[str, ...]:
    return tuple(values or ())


@dataclass(frozen=True)
class PathsConfig:
    """Configuration for source paths and exclusion patterns."""

    roots: tuple[str, ...]
    exclude_globs: tuple[str, ...]


@dataclass(frozen=True)
class Flake8Config:
    """Configuration options for flake8 linter."""

    extend_ignore: tuple[str, ...]
    exclude: tuple[str, ...]


@dataclass(frozen=True)
class PylintConfig:
    """Configuration options for pylint linter."""

    disable: tuple[str, ...]
    ignore: tuple[str, ...]
    ignore_paths: tuple[str, ...]
    ignore_patterns: tuple[str, ...]


@dataclass(frozen=True)
class PyrightConfig:
    """Configuration options for pyright type checker."""

    exclude: tuple[str, ...]
    extra_paths: tuple[str, ...]


@dataclass(frozen=True)
class PycodestyleConfig:
    """Configuration options for pycodestyle checker."""

    ignore: tuple[str, ...]


@dataclass(frozen=True)
class RunnerConfig:
    """Configuration for the lint runner execution."""

    auto_targets: tuple[str, ...]
    pylint_disable: tuple[str, ...]


@dataclass(frozen=True)
class LintConfig:  # pylint: disable=too-many-instance-attributes
    """Root configuration container for all linting tools."""

    line_length: int
    vendor_globs: tuple[str, ...]
    paths: PathsConfig
    flake8: Flake8Config
    pylint: PylintConfig
    pyright: PyrightConfig
    pycodestyle: PycodestyleConfig
    runner: RunnerConfig


@lru_cache(maxsize=1)
def load_config() -> LintConfig:
    """Load and parse the lint configuration from config.yaml."""
    payload = _load_yaml_payload()
    # Navigate to contexts.python.lint
    ctx = payload.get("contexts", {}).get("python", {})
    raw = ctx.get("lint", {})

    vendor = _as_tuple(raw.get("vendor_globs"))
    paths = raw.get("paths", {})
    flake8 = raw.get("flake8", {})
    pylint = raw.get("pylint", {})
    pyright = raw.get("pyright", {})
    pycodestyle = raw.get("pycodestyle", {})
    runner = raw.get("runner", {})

    return LintConfig(
        line_length=int(raw.get("line_length", 120)),
        vendor_globs=vendor,
        paths=PathsConfig(
            roots=_as_tuple(paths.get("lint_roots")),
            exclude_globs=_as_tuple(paths.get("exclude_globs")) or vendor,
        ),
        flake8=Flake8Config(
            extend_ignore=_as_tuple(flake8.get("extend_ignore")),
            exclude=_as_tuple(flake8.get("exclude")) or vendor,
        ),
        pylint=PylintConfig(
            disable=_as_tuple(pylint.get("disable")),
            ignore=_as_tuple(pylint.get("ignore")),
            ignore_paths=_as_tuple(pylint.get("ignore_paths")),
            ignore_patterns=_as_tuple(pylint.get("ignore_patterns")),
        ),
        pyright=PyrightConfig(
            exclude=_as_tuple(pyright.get("exclude")) or vendor,
            extra_paths=_as_tuple(pyright.get("extra_paths")),
        ),
        pycodestyle=PycodestyleConfig(
            ignore=_as_tuple(pycodestyle.get("ignore"))
            or _as_tuple(flake8.get("extend_ignore"))
        ),
        runner=RunnerConfig(
            auto_targets=_as_tuple(runner.get("auto_targets"))
            or _as_tuple(paths.get("lint_roots")),
            pylint_disable=_as_tuple(runner.get("pylint_disable"))
            or _as_tuple(pylint.get("disable")),
        ),
    )


def _load_yaml_payload() -> dict[str, Any]:
    if not CONFIG_PATH.exists():
        return {}
    return yaml.safe_load(CONFIG_PATH.read_text(encoding="utf-8")) or {}
