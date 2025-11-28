#!/usr/bin/env python3
"""Shared lint-configuration loader and helpers."""

from __future__ import annotations

import fnmatch
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Any, Iterable, Sequence

try:
    import yaml
except ImportError as exc:  # pragma: no cover
    raise RuntimeError(
        "PyYAML is required to load .config/config.yaml with anchors/tags"
    ) from exc

REPO_ROOT = Path(__file__).resolve().parents[1]
CONFIG_ROOT = REPO_ROOT / ".config"
CONFIG_PATH = CONFIG_ROOT / "config.yaml"
CONTEXT_CHAIN = ("contexts", "python", "lint")


def _as_tuple(values: Sequence[str] | None) -> tuple[str, ...]:
    return tuple(values or ())


@dataclass(frozen=True)
class PathsConfig:
    roots: tuple[str, ...]
    exclude_globs: tuple[str, ...]


@dataclass(frozen=True)
class Flake8Config:
    extend_ignore: tuple[str, ...]
    exclude: tuple[str, ...]


@dataclass(frozen=True)
class PylintConfig:
    disable: tuple[str, ...]
    ignore: tuple[str, ...]
    ignore_paths: tuple[str, ...]
    ignore_patterns: tuple[str, ...]


@dataclass(frozen=True)
class PyrightConfig:
    exclude: tuple[str, ...]


@dataclass(frozen=True)
class PycodestyleConfig:
    ignore: tuple[str, ...]


@dataclass(frozen=True)
class RunnerConfig:
    auto_targets: tuple[str, ...]
    pylint_disable: tuple[str, ...]


@dataclass(frozen=True)
class LintConfig:
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
    payload = yaml.safe_load(CONFIG_PATH.read_text(encoding="utf-8")) or {}
    raw = _resolve_context(payload)
    vendor = _as_tuple(raw.get("vendor_globs"))
    paths = raw.get("paths", {})
    flake8 = raw.get("flake8", {})
    pylint = raw.get("pylint", {})
    pyright = raw.get("pyright", {})
    pycodestyle = raw.get("pycodestyle", {})
    runner = raw.get("runner", {})
    return LintConfig(
        line_length=int(raw.get("line_length", 100)),
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
        pyright=PyrightConfig(exclude=_as_tuple(pyright.get("exclude")) or vendor),
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


def _resolve_context(payload: dict[str, Any]) -> dict[str, Any]:
    node: Any = payload
    for key in CONTEXT_CHAIN:
        if not isinstance(node, dict) or key not in node:
            raise KeyError(
                "Missing python.lint context in .config/config.yaml; rerun lint bootstrap."
            )
        node = node[key]
    if not isinstance(node, dict):
        raise TypeError("python.lint context in .config/config.yaml must be a mapping.")
    return node


def collect_existing_roots(cfg: LintConfig) -> list[str]:
    """Return repo-relative directories that actually exist."""
    existing: list[str] = []
    source = cfg.runner.auto_targets or cfg.paths.roots
    for relative in source:
        candidate = REPO_ROOT / relative
        if candidate.exists():
            existing.append(str(candidate))
    return existing


def normalize_for_matching(path: str | Path) -> str:
    candidate = Path(path)
    try:
        candidate = candidate.relative_to(REPO_ROOT)
    except ValueError:
        pass
    return candidate.as_posix().lower()


def is_vendor_path(path: str | Path, vendor_patterns: Iterable[str]) -> bool:
    """Check whether a path matches any vendor glob pattern."""
    normalized = normalize_for_matching(path)
    for pattern in vendor_patterns:
        if fnmatch.fnmatch(normalized, pattern.lower()):
            return True
    return False
