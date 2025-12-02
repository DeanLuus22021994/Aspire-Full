"""
Abstracts dependencies from the Python virtual environment to local vendor modules.

This script scans the mypy cache for type information and generates protocol-based
abstractions in the _vendor directory, enabling static type checking without
requiring packages to be installed.

Usage:
    python abstract_dependency_report.py [--packages PKG1,PKG2] [--output-dir DIR]
"""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass, field
from datetime import datetime
from enum import StrEnum
from pathlib import Path
from typing import Any, Final

import yaml

# ============================================================================
# Constants
# ============================================================================

MYPY_CACHE_DIR: Final[str] = ".cache/mypy/3.15"
VENDOR_DIR: Final[str] = "src/aspire_agents/_vendor"
REPORT_FILE: Final[str] = "vendor-abstractions.yaml"

# Packages to abstract by default
DEFAULT_PACKAGES: Final[list[str]] = [
    "torch",
    "transformers",
    "openai",
    "agents",
    "safetensors",
    "redis",
    "_ctypes",
    "cProfile",
    "pstats",
    "profile",
    "threading",
]


# ============================================================================
# Enums
# ============================================================================


class AbstractionStatus(StrEnum):
    """Status of a package abstraction."""

    PENDING = "pending"
    COMPLETED = "completed"
    FAILED = "failed"
    SKIPPED = "skipped"
    NOT_FOUND = "not_found"


class TypeCategory(StrEnum):
    """Category of extracted type."""

    CLASS = "class"
    FUNCTION = "function"
    VARIABLE = "variable"
    TYPE_ALIAS = "type_alias"
    PROTOCOL = "protocol"
    EXCEPTION = "exception"
    ENUM = "enum"
    DATACLASS = "dataclass"


# ============================================================================
# Data Classes
# ============================================================================


@dataclass
class ExtractedType:
    """Represents an extracted type from mypy cache."""

    name: str
    category: TypeCategory
    module: str
    bases: list[str] = field(default_factory=list)
    methods: list[str] = field(default_factory=list)
    attributes: list[str] = field(default_factory=list)
    signature: str | None = None


@dataclass
class PackageAbstraction:
    """Represents a package abstraction result."""

    name: str
    status: AbstractionStatus
    cache_path: str | None = None
    vendor_file: str | None = None
    types_extracted: int = 0
    types: list[ExtractedType] = field(default_factory=list)
    error: str | None = None


@dataclass
class AbstractionReport:
    """Complete abstraction report."""

    generated_at: str
    mypy_cache_dir: str
    vendor_dir: str
    python_version: str
    packages: list[PackageAbstraction] = field(default_factory=list)

    @property
    def summary(self) -> dict[str, int]:
        """Generate summary statistics."""
        return {
            "total_packages": len(self.packages),
            "completed": sum(
                1 for p in self.packages if p.status == AbstractionStatus.COMPLETED
            ),
            "pending": sum(
                1 for p in self.packages if p.status == AbstractionStatus.PENDING
            ),
            "failed": sum(
                1 for p in self.packages if p.status == AbstractionStatus.FAILED
            ),
            "not_found": sum(
                1 for p in self.packages if p.status == AbstractionStatus.NOT_FOUND
            ),
            "total_types": sum(p.types_extracted for p in self.packages),
        }


# ============================================================================
# Cache Parser
# ============================================================================


class MypyCacheParser:
    """Parses mypy cache JSON files to extract type information."""

    def __init__(self, cache_dir: Path) -> None:
        self.cache_dir = cache_dir

    def find_package_cache(self, package: str) -> Path | None:
        """Find the cache directory/file for a package."""
        # Try direct module file
        direct_file = self.cache_dir / f"{package}.data.json"
        if direct_file.exists():
            return direct_file

        # Try package directory
        pkg_dir = self.cache_dir / package
        if pkg_dir.is_dir():
            init_file = pkg_dir / "__init__.data.json"
            if init_file.exists():
                return init_file

        # Try with underscore prefix (internal modules)
        prefixed_file = self.cache_dir / f"_{package}.data.json"
        if prefixed_file.exists():
            return prefixed_file

        return None

    def parse_cache_file(self, cache_path: Path) -> list[ExtractedType]:
        """Parse a mypy cache file and extract type definitions."""
        try:
            with cache_path.open("r", encoding="utf-8") as f:
                data = json.load(f)
        except (json.JSONDecodeError, OSError) as e:
            print(f"  Warning: Failed to parse {cache_path}: {e}", file=sys.stderr)
            return []

        types: list[ExtractedType] = []
        names = data.get("names", {})

        if isinstance(names, dict) and names.get(".class") == "SymbolTable":
            for name, symbol in names.items():
                if name.startswith((".", "__")) and name not in (
                    "__init__",
                    "__enter__",
                    "__exit__",
                ):
                    continue

                extracted = self._extract_symbol(name, symbol, data.get("_fullname", ""))
                if extracted:
                    types.append(extracted)

        return types

    def _extract_symbol(
        self, name: str, symbol: dict[str, Any], module: str
    ) -> ExtractedType | None:
        """Extract a single symbol from the cache."""
        if not isinstance(symbol, dict):
            return None

        kind = symbol.get("kind")
        node = symbol.get("node")

        if not node or not isinstance(node, dict):
            # Check for cross-reference
            if symbol.get("cross_ref"):
                return None
            return None

        node_class = node.get(".class")

        # Handle TypeInfo (classes)
        if node_class == "TypeInfo":
            return self._extract_class(name, node, module)

        # Handle FuncDef (functions)
        if node_class == "FuncDef":
            return self._extract_function(name, node, module)

        # Handle Var (variables/constants)
        if node_class == "Var":
            return self._extract_variable(name, node, module)

        # Handle TypeAlias
        if node_class == "TypeAlias":
            return ExtractedType(
                name=name,
                category=TypeCategory.TYPE_ALIAS,
                module=module,
            )

        # Handle Decorator (decorated functions)
        if node_class == "Decorator":
            func = node.get("func", {})
            if func.get(".class") == "FuncDef":
                return self._extract_function(name, func, module)

        return None

    def _extract_class(
        self, name: str, node: dict[str, Any], module: str
    ) -> ExtractedType:
        """Extract class information."""
        bases = node.get("bases", [])
        base_names = []
        for base in bases:
            if isinstance(base, str):
                base_names.append(base.split(".")[-1])

        # Determine category
        flags = node.get("flags", [])
        metadata = node.get("metadata", {})

        category = TypeCategory.CLASS
        if "is_enum" in flags:
            category = TypeCategory.ENUM
        elif "dataclass" in metadata:
            category = TypeCategory.DATACLASS
        elif any("Exception" in b or "Error" in b for b in base_names):
            category = TypeCategory.EXCEPTION

        # Extract methods and attributes
        methods: list[str] = []
        attributes: list[str] = []

        class_names = node.get("names", {})
        if isinstance(class_names, dict):
            for member_name, member in class_names.items():
                if member_name.startswith("."):
                    continue
                if isinstance(member, dict):
                    member_node = member.get("node", {})
                    if isinstance(member_node, dict):
                        if member_node.get(".class") in ("FuncDef", "OverloadedFuncDef"):
                            methods.append(member_name)
                        elif member_node.get(".class") == "Var":
                            attributes.append(member_name)

        return ExtractedType(
            name=name,
            category=category,
            module=module,
            bases=base_names,
            methods=methods,
            attributes=attributes,
        )

    def _extract_function(
        self, name: str, node: dict[str, Any], module: str
    ) -> ExtractedType:
        """Extract function information."""
        arg_names = node.get("arg_names", [])
        signature = f"({', '.join(str(a) for a in arg_names if a)})"

        return ExtractedType(
            name=name,
            category=TypeCategory.FUNCTION,
            module=module,
            signature=signature,
        )

    def _extract_variable(
        self, name: str, node: dict[str, Any], module: str
    ) -> ExtractedType:
        """Extract variable/constant information."""
        return ExtractedType(
            name=name,
            category=TypeCategory.VARIABLE,
            module=module,
        )


# ============================================================================
# Vendor Status Checker
# ============================================================================


class VendorStatusChecker:
    """Checks the status of vendor abstractions."""

    VENDOR_FILE_MAP: Final[dict[str, str]] = {
        "torch": "_torch.py",
        "transformers": "_transformers.py",
        "openai": "_openai.py",
        "agents": "_agents.py",
        "threading": "_threading.py",
        "safetensors": "_safetensors.py",
        "redis": "_redis.py",
        "_ctypes": "_ctypes.py",
        "ctypes": "_ctypes.py",
        "cProfile": "_profiler.py",
        "pstats": "_profiler.py",
        "profile": "_profiler.py",
    }

    def __init__(self, vendor_dir: Path) -> None:
        self.vendor_dir = vendor_dir

    def get_vendor_file(self, package: str) -> str | None:
        """Get the vendor file name for a package."""
        return self.VENDOR_FILE_MAP.get(package)

    def check_status(self, package: str) -> AbstractionStatus:
        """Check if a vendor abstraction exists and is valid."""
        vendor_file = self.get_vendor_file(package)
        if not vendor_file:
            return AbstractionStatus.PENDING

        vendor_path = self.vendor_dir / vendor_file
        if vendor_path.exists():
            # Check if file has content beyond just imports
            content = vendor_path.read_text(encoding="utf-8")
            if len(content) > 200:  # Reasonable threshold
                return AbstractionStatus.COMPLETED

        return AbstractionStatus.PENDING


# ============================================================================
# Abstraction Generator
# ============================================================================


class AbstractionGenerator:
    """Generates the abstraction report and coordinates the process."""

    def __init__(
        self,
        base_dir: Path,
        packages: list[str] | None = None,
    ) -> None:
        self.base_dir = base_dir
        self.packages = packages or DEFAULT_PACKAGES
        self.cache_dir = base_dir / MYPY_CACHE_DIR
        self.vendor_dir = base_dir / VENDOR_DIR

        self.parser = MypyCacheParser(self.cache_dir)
        self.checker = VendorStatusChecker(self.vendor_dir)

    def generate_report(self) -> AbstractionReport:
        """Generate the complete abstraction report."""
        report = AbstractionReport(
            generated_at=datetime.now().isoformat(),
            mypy_cache_dir=str(self.cache_dir),
            vendor_dir=str(self.vendor_dir),
            python_version="3.15.0",
        )

        for package in self.packages:
            abstraction = self._process_package(package)
            report.packages.append(abstraction)

        return report

    def _process_package(self, package: str) -> PackageAbstraction:
        """Process a single package for abstraction."""
        cache_path = self.parser.find_package_cache(package)

        if not cache_path:
            return PackageAbstraction(
                name=package,
                status=AbstractionStatus.NOT_FOUND,
                error=f"No mypy cache found for '{package}'",
            )

        # Check vendor status
        status = self.checker.check_status(package)
        vendor_file = self.checker.get_vendor_file(package)

        # Parse cache to extract types
        try:
            types = self.parser.parse_cache_file(cache_path)
        except Exception as e:
            return PackageAbstraction(
                name=package,
                status=AbstractionStatus.FAILED,
                cache_path=str(cache_path),
                error=str(e),
            )

        return PackageAbstraction(
            name=package,
            status=status,
            cache_path=str(cache_path),
            vendor_file=vendor_file,
            types_extracted=len(types),
            types=types,
        )

    def save_report(self, report: AbstractionReport, output_path: Path) -> None:
        """Save the report to YAML."""
        report_dict = {
            "generated_at": report.generated_at,
            "python_version": report.python_version,
            "mypy_cache_dir": report.mypy_cache_dir,
            "vendor_dir": report.vendor_dir,
            "summary": report.summary,
            "packages": [
                {
                    "name": p.name,
                    "status": p.status.value,
                    "cache_path": p.cache_path,
                    "vendor_file": p.vendor_file,
                    "types_extracted": p.types_extracted,
                    "types": [
                        {
                            "name": t.name,
                            "category": t.category.value,
                            "bases": t.bases if t.bases else None,
                            "methods": t.methods[:10] if t.methods else None,  # Limit
                            "attributes": t.attributes[:10] if t.attributes else None,
                        }
                        for t in p.types[:20]  # Limit types per package
                    ]
                    if p.types
                    else None,
                    "error": p.error,
                }
                for p in report.packages
            ],
        }

        # Clean None values
        def clean_dict(d: Any) -> Any:
            if isinstance(d, dict):
                return {k: clean_dict(v) for k, v in d.items() if v is not None}
            if isinstance(d, list):
                return [clean_dict(i) for i in d]
            return d

        report_dict = clean_dict(report_dict)

        with output_path.open("w", encoding="utf-8") as f:
            yaml.dump(report_dict, f, sort_keys=False, default_flow_style=False)

    def print_summary(self, report: AbstractionReport) -> None:
        """Print a summary to stdout."""
        summary = report.summary
        print("\n" + "=" * 60)
        print("VENDOR ABSTRACTION REPORT")
        print("=" * 60)
        print(f"Generated: {report.generated_at}")
        print(f"Python Version: {report.python_version}")
        print(f"Cache Dir: {report.mypy_cache_dir}")
        print(f"Vendor Dir: {report.vendor_dir}")
        print("-" * 60)
        print(f"Total Packages: {summary['total_packages']}")
        print(f"  Completed: {summary['completed']}")
        print(f"  Pending: {summary['pending']}")
        print(f"  Not Found: {summary['not_found']}")
        print(f"  Failed: {summary['failed']}")
        print(f"Total Types Extracted: {summary['total_types']}")
        print("-" * 60)

        for pkg in report.packages:
            status_icon = {
                AbstractionStatus.COMPLETED: "✓",
                AbstractionStatus.PENDING: "○",
                AbstractionStatus.NOT_FOUND: "?",
                AbstractionStatus.FAILED: "✗",
                AbstractionStatus.SKIPPED: "-",
            }.get(pkg.status, "?")

            print(f"  {status_icon} {pkg.name}: {pkg.status.value} ({pkg.types_extracted} types)")
            if pkg.error:
                print(f"      Error: {pkg.error}")

        print("=" * 60 + "\n")


# ============================================================================
# Main Entry Point
# ============================================================================


def main() -> int:
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Abstract dependencies from venv to local vendor modules."
    )
    parser.add_argument(
        "--packages",
        type=str,
        help="Comma-separated list of packages to abstract",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("."),
        help="Output directory for the report",
    )
    parser.add_argument(
        "--base-dir",
        type=Path,
        default=Path(__file__).parent.parent,
        help="Base directory of the python-agents project",
    )

    args = parser.parse_args()

    packages = None
    if args.packages:
        packages = [p.strip() for p in args.packages.split(",")]

    generator = AbstractionGenerator(
        base_dir=args.base_dir,
        packages=packages,
    )

    print("Scanning mypy cache and vendor abstractions...")
    report = generator.generate_report()

    output_path = args.output_dir / REPORT_FILE
    generator.save_report(report, output_path)
    generator.print_summary(report)

    print(f"Report saved to: {output_path}")

    # Return non-zero if there are pending abstractions
    if report.summary["pending"] > 0:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
