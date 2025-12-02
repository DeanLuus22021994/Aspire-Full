"""
Generates a dependency report for the Python environment.

This script uses `uv` to list installed and outdated packages and generates
a YAML report containing the status of each package.
"""

from __future__ import annotations

import importlib
import json
import subprocess
import sys
from datetime import datetime
from typing import Any, TypedDict

# Dynamic import for yaml - types-PyYAML stub not required
_yaml_module: Any = importlib.import_module("yaml")


class InstalledPackage(TypedDict):
    """Represents an installed package from uv pip list."""

    name: str
    version: str


class OutdatedPackage(TypedDict):
    """Represents an outdated package from uv pip list --outdated."""

    name: str
    version: str
    latest_version: str
    type: str


class ReportPackageInfo(TypedDict, total=False):
    """Represents package information in the generated report."""

    name: str
    current_version: str
    status: str
    latest_version: str
    update_type: str


class Summary(TypedDict):
    """Summary of the dependency report."""

    total_packages: int
    outdated_packages: int


class ReportData(TypedDict):
    """Structure of the final report data."""

    generated_at: str
    summary: Summary
    packages: list[ReportPackageInfo]


def get_installed_packages() -> list[InstalledPackage]:
    """
    Retrieves a list of installed packages using `uv pip list`.

    Returns:
        list[InstalledPackage]: A list of dictionaries containing package details.
    """
    try:
        result = subprocess.run(
            ["uv", "pip", "list", "--format=json"],
            capture_output=True,
            text=True,
            check=True,
        )
        data: list[InstalledPackage] = json.loads(result.stdout)
        return data
    except subprocess.CalledProcessError as e:
        print(f"Error getting installed packages: {e}")
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"Error decoding JSON from uv pip list: {e}")
        sys.exit(1)


def get_outdated_packages() -> list[OutdatedPackage]:
    """
    Retrieves a list of outdated packages using `uv pip list --outdated`.

    Returns:
        list[OutdatedPackage]: A list of dictionaries containing outdated package details.
    """
    try:
        # uv pip list --outdated returns a table, not JSON currently in all versions,
        # but let's try to parse it or use a different approach.
        # Actually, uv pip list --outdated --format=json is supported in newer versions.
        result = subprocess.run(
            ["uv", "pip", "list", "--outdated", "--format=json"],
            capture_output=True,
            text=True,
            check=True,
        )
        data: list[OutdatedPackage] = json.loads(result.stdout)
        return data
    except subprocess.CalledProcessError:
        # Fallback if json format not supported or other error
        return []
    except json.JSONDecodeError:
        return []


def generate_report() -> None:
    """
    Generates the dependency report and saves it to `python-deps.config.yaml`.
    """
    installed = get_installed_packages()
    outdated = get_outdated_packages()

    outdated_map: dict[str, OutdatedPackage] = {pkg["name"]: pkg for pkg in outdated}

    report_packages: list[ReportPackageInfo] = []

    for pkg in installed:
        name = pkg["name"]
        version = pkg["version"]

        pkg_info: ReportPackageInfo = {
            "name": name,
            "current_version": version,
            "status": "current",
        }

        if name in outdated_map:
            outdated_pkg = outdated_map[name]
            pkg_info["status"] = "outdated"
            pkg_info["latest_version"] = outdated_pkg["latest_version"]
            pkg_info["update_type"] = outdated_pkg.get("type", "unknown")

        report_packages.append(pkg_info)

    summary: Summary = {
        "total_packages": len(installed),
        "outdated_packages": len(outdated),
    }

    report_data: ReportData = {
        "generated_at": datetime.now().isoformat(),
        "summary": summary,
        "packages": report_packages,
    }

    with open("python-deps.config.yaml", "w", encoding="utf-8") as f:
        _yaml_module.dump(report_data, f, sort_keys=False)

    print("Report generated: python-deps.config.yaml")


if __name__ == "__main__":
    generate_report()
