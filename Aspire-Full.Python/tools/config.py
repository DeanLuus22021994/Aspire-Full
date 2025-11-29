#!/usr/bin/env python3
"""Shared lint/test configuration loader and helpers."""

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
        "PyYAML is required to load python-lint.yaml with anchors/tags"
    ) from exc

# Aspire-Full.Python/tools/config.py -> parents[0]=tools, parents[1]=Aspire-Full.Python, parents[2]=Root
REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECT_ROOT = Path(__file__).resolve().parents[1]
CONFIG_PATH = PROJECT_ROOT / "python-lint.yaml"


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
    extra_paths: tuple[str, ...]


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
    if not CONFIG_PATH.exists():
        # Fallback or empty if not generated yet
        return LintConfig(
            line_length=100,
            vendor_globs=(),
            paths=PathsConfig((), ()),
            flake8=Flake8Config((), ()),
            pylint=PylintConfig((), (), (), ()),
            pyright=PyrightConfig((), ()),
            pycodestyle=PycodestyleConfig(()),
            runner=RunnerConfig((), ()),
        )

    payload = yaml.safe_load(CONFIG_PATH.read_text(encoding="utf-8")) or {}

    # The payload is now flat, matching the LintConfig structure directly
    # But we still need to map dicts to dataclasses safely

    vendor = _as_tuple(payload.get("vendor_globs"))
    paths = payload.get("paths", {})
    flake8 = payload.get("flake8", {})
    pylint = payload.get("pylint", {})
    pyright = payload.get("pyright", {})
    pycodestyle = payload.get("pycodestyle", {})
    runner = payload.get("runner", {})

    return LintConfig(
        line_length=int(payload.get("line_length", 100)),
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
