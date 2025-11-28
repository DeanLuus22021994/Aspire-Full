#!/usr/bin/env python3
"""Shared lint-configuration loader and helpers."""

from __future__ import annotations

import fnmatch
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Iterable, Sequence

try:
    import yaml
except ImportError as exc:  # pragma: no cover
    raise RuntimeError(
        "PyYAML is required to load .config/python-lint.yaml with anchors/tags"
    ) from exc

REPO_ROOT = Path(__file__).resolve().parents[2]
CONFIG_PATH = REPO_ROOT / ".config" / "python-lint.yaml"


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


@dataclass(frozen=True)
class PyrightConfig:
    exclude: tuple[str, ...]


@dataclass(frozen=True)
class LintConfig:
    line_length: int
    vendor_globs: tuple[str, ...]
    paths: PathsConfig
    flake8: Flake8Config
    pylint: PylintConfig
    pyright: PyrightConfig


@lru_cache(maxsize=1)
def load_config() -> LintConfig:
    raw = yaml.safe_load(CONFIG_PATH.read_text(encoding="utf-8"))
    vendor = _as_tuple(raw.get("vendor_globs"))
    paths = raw.get("paths", {})
    flake8 = raw.get("flake8", {})
    pylint = raw.get("pylint", {})
    pyright = raw.get("pyright", {})
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
        ),
        pyright=PyrightConfig(exclude=_as_tuple(pyright.get("exclude")) or vendor),
    )


def collect_existing_roots(cfg: LintConfig) -> list[str]:
    """Return repo-relative directories that actually exist."""
    existing: list[str] = []
    for relative in cfg.paths.roots:
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
