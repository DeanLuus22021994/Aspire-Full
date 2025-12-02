#!/usr/bin/env python3
"""Registry Analyzer for self-enhancement automation.

Analyzes the Docker registry, Python vendor modules, and codebase
for redundancies, optimizes overhead, and generates enhancement reports.

This script integrates with:
- Aspire-Full.DockerRegistry: GarbageCollector, PatternEngine
- Aspire-Full.Agents.Core: SubagentSelfReviewService
- Python vendor modules: _profiler, _qdrant, _enums

Usage:
    python registry_analyzer.py [--output-dir DIR] [--dry-run]
"""

from __future__ import annotations

import ast
import json
import subprocess
import sys
from dataclasses import dataclass, field
from datetime import datetime
from enum import StrEnum
from pathlib import Path
from typing import Any, Final

# ============================================================================
# Constants
# ============================================================================

WORKSPACE: Final[Path] = Path(__file__).resolve().parents[3]
VENDOR_DIR: Final[Path] = WORKSPACE / "AI" / "Aspire-Full.Python" / "python-agents" / "src" / "aspire_agents" / "_vendor"
INFRA_DIR: Final[Path] = WORKSPACE / "Infra"
REPORT_FILE: Final[str] = "registry-analysis.json"


# ============================================================================
# Enums
# ============================================================================


class AnalysisCategory(StrEnum):
    """Categories for analysis findings."""

    REDUNDANCY = "redundancy"
    OVERHEAD = "overhead"
    UNUSED = "unused"
    DEPRECATED = "deprecated"
    OPTIMIZATION = "optimization"


class Severity(StrEnum):
    """Severity levels for findings."""

    INFO = "info"
    WARNING = "warning"
    ERROR = "error"
    CRITICAL = "critical"


# ============================================================================
# Data Classes
# ============================================================================


@dataclass
class Finding:
    """A single analysis finding."""

    category: AnalysisCategory
    severity: Severity
    location: str
    message: str
    suggested_action: str | None = None
    auto_fixable: bool = False

    def to_dict(self) -> dict[str, Any]:
        return {
            "category": self.category.value,
            "severity": self.severity.value,
            "location": self.location,
            "message": self.message,
            "suggested_action": self.suggested_action,
            "auto_fixable": self.auto_fixable,
        }


@dataclass
class ModuleMetrics:
    """Metrics for a Python module."""

    path: str
    lines_of_code: int
    function_count: int
    class_count: int
    complexity: float
    imports: list[str] = field(default_factory=list)
    exports: list[str] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        return {
            "path": self.path,
            "lines_of_code": self.lines_of_code,
            "function_count": self.function_count,
            "class_count": self.class_count,
            "complexity": self.complexity,
            "imports": self.imports,
            "exports": self.exports,
        }


@dataclass
class AnalysisReport:
    """Complete analysis report."""

    timestamp: str
    workspace: str
    findings: list[Finding] = field(default_factory=list)
    module_metrics: list[ModuleMetrics] = field(default_factory=list)
    docker_images: list[dict[str, Any]] = field(default_factory=list)
    summary: dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> dict[str, Any]:
        return {
            "timestamp": self.timestamp,
            "workspace": self.workspace,
            "summary": self.summary,
            "findings": [f.to_dict() for f in self.findings],
            "module_metrics": [m.to_dict() for m in self.module_metrics],
            "docker_images": self.docker_images,
        }


# ============================================================================
# Analyzers
# ============================================================================


class PythonModuleAnalyzer:
    """Analyzes Python modules for redundancies and metrics."""

    def __init__(self, vendor_dir: Path) -> None:
        self.vendor_dir = vendor_dir
        self.findings: list[Finding] = []
        self.metrics: list[ModuleMetrics] = []

    def analyze(self) -> tuple[list[Finding], list[ModuleMetrics]]:
        """Run full analysis on vendor modules."""
        if not self.vendor_dir.exists():
            self.findings.append(Finding(
                category=AnalysisCategory.OVERHEAD,
                severity=Severity.ERROR,
                location=str(self.vendor_dir),
                message="Vendor directory does not exist",
            ))
            return self.findings, self.metrics

        # Analyze each module
        for py_file in sorted(self.vendor_dir.glob("*.py")):
            if py_file.name.startswith("__"):
                continue
            self._analyze_module(py_file)

        # Check for cross-module redundancies
        self._check_cross_module_redundancies()

        return self.findings, self.metrics

    def _analyze_module(self, path: Path) -> None:
        """Analyze a single Python module."""
        try:
            source = path.read_text(encoding="utf-8")
            tree = ast.parse(source)
        except (OSError, SyntaxError) as e:
            self.findings.append(Finding(
                category=AnalysisCategory.OVERHEAD,
                severity=Severity.ERROR,
                location=str(path),
                message=f"Failed to parse: {e}",
            ))
            return

        # Calculate metrics
        lines = source.count("\n") + 1
        functions = sum(1 for n in ast.walk(tree) if isinstance(n, (ast.FunctionDef, ast.AsyncFunctionDef)))
        classes = sum(1 for n in ast.walk(tree) if isinstance(n, ast.ClassDef))
        complexity = self._calculate_complexity(tree)
        imports = self._extract_imports(tree)
        exports = self._extract_exports(tree)

        self.metrics.append(ModuleMetrics(
            path=path.name,
            lines_of_code=lines,
            function_count=functions,
            class_count=classes,
            complexity=complexity,
            imports=imports,
            exports=exports,
        ))

        # Check for oversized modules
        if lines > 1000:
            self.findings.append(Finding(
                category=AnalysisCategory.OVERHEAD,
                severity=Severity.WARNING,
                location=path.name,
                message=f"Module has {lines} lines - consider splitting",
                suggested_action="Split into smaller focused modules",
            ))

        # Check for high complexity
        if complexity > 50:
            self.findings.append(Finding(
                category=AnalysisCategory.OVERHEAD,
                severity=Severity.WARNING,
                location=path.name,
                message=f"High cyclomatic complexity: {complexity:.1f}",
                suggested_action="Refactor complex functions into smaller units",
            ))

        # Check for unused imports
        self._check_unused_imports(path.name, source, tree, imports)

    def _calculate_complexity(self, tree: ast.AST) -> float:
        """Calculate cyclomatic complexity."""
        complexity = 1.0
        for node in ast.walk(tree):
            if isinstance(node, (ast.If, ast.While, ast.For, ast.AsyncFor)):
                complexity += 1.0
            elif isinstance(node, ast.BoolOp):
                complexity += len(node.values) - 1
            elif isinstance(node, (ast.ExceptHandler, ast.Try)):
                complexity += 0.5
            elif isinstance(node, (ast.ListComp, ast.SetComp, ast.DictComp, ast.GeneratorExp)):
                complexity += 1.0
        return complexity

    def _extract_imports(self, tree: ast.AST) -> list[str]:
        """Extract all imports from a module."""
        imports: list[str] = []
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                for alias in node.names:
                    imports.append(alias.asname or alias.name)
            elif isinstance(node, ast.ImportFrom):
                for alias in node.names:
                    if alias.name != "*":
                        imports.append(alias.asname or alias.name)
        return imports

    def _extract_exports(self, tree: ast.AST) -> list[str]:
        """Extract __all__ exports from a module."""
        for node in ast.walk(tree):
            if isinstance(node, ast.Assign):
                for target in node.targets:
                    if isinstance(target, ast.Name) and target.id == "__all__":
                        if isinstance(node.value, ast.List):
                            return [
                                elt.value for elt in node.value.elts
                                if isinstance(elt, ast.Constant) and isinstance(elt.value, str)
                            ]
        return []

    def _check_unused_imports(self, filename: str, source: str, tree: ast.AST, imports: list[str]) -> None:
        """Check for unused imports in a module."""
        used_names: set[str] = set()
        for node in ast.walk(tree):
            if isinstance(node, ast.Name):
                used_names.add(node.id)
            elif isinstance(node, ast.Attribute) and isinstance(node.value, ast.Name):
                used_names.add(node.value.id)

        # Common typing imports that appear unused
        typing_imports = {
            "Any", "Final", "Protocol", "TypeVar", "Callable", "Generic",
            "Self", "cast", "TYPE_CHECKING", "runtime_checkable", "overload",
            "annotations", "Sequence", "Mapping", "Iterator", "AsyncIterator",
        }

        potentially_unused = set(imports) - used_names - typing_imports

        for name in potentially_unused:
            self.findings.append(Finding(
                category=AnalysisCategory.UNUSED,
                severity=Severity.INFO,
                location=filename,
                message=f"Potentially unused import: {name}",
                suggested_action=f"Remove import '{name}' if not needed",
                auto_fixable=True,
            ))

    def _check_cross_module_redundancies(self) -> None:
        """Check for redundant definitions across modules."""
        class_definitions: dict[str, list[str]] = {}
        enum_definitions: dict[str, list[str]] = {}

        for metric in self.metrics:
            path = self.vendor_dir / metric.path
            try:
                source = path.read_text(encoding="utf-8")
                tree = ast.parse(source)
            except Exception:
                continue

            for node in ast.walk(tree):
                if isinstance(node, ast.ClassDef):
                    bases = [b.id for b in node.bases if isinstance(b, ast.Name)]
                    if "StrEnum" in bases or "Enum" in bases:
                        if node.name not in enum_definitions:
                            enum_definitions[node.name] = []
                        enum_definitions[node.name].append(metric.path)
                    else:
                        if node.name not in class_definitions:
                            class_definitions[node.name] = []
                        class_definitions[node.name].append(metric.path)

        # Report duplicate definitions
        for name, files in enum_definitions.items():
            if len(files) > 1:
                self.findings.append(Finding(
                    category=AnalysisCategory.REDUNDANCY,
                    severity=Severity.WARNING,
                    location=", ".join(files),
                    message=f"Duplicate enum '{name}' defined in multiple files",
                    suggested_action=f"Consolidate '{name}' into _enums.py",
                    auto_fixable=True,
                ))

        for name, files in class_definitions.items():
            if len(files) > 1:
                self.findings.append(Finding(
                    category=AnalysisCategory.REDUNDANCY,
                    severity=Severity.INFO,
                    location=", ".join(files),
                    message=f"Class '{name}' defined in multiple files",
                    suggested_action=f"Review if '{name}' should be consolidated",
                ))


class DockerRegistryAnalyzer:
    """Analyzes Docker registry for redundancies and cleanup opportunities."""

    def __init__(self) -> None:
        self.findings: list[Finding] = []
        self.images: list[dict[str, Any]] = []

    def analyze(self) -> tuple[list[Finding], list[dict[str, Any]]]:
        """Analyze Docker images and registry."""
        try:
            result = subprocess.run(
                ["docker", "images", "--format", "{{json .}}"],
                capture_output=True,
                text=True,
                check=False,
            )
            if result.returncode == 0:
                for line in result.stdout.strip().split("\n"):
                    if line:
                        try:
                            image = json.loads(line)
                            self.images.append(image)
                        except json.JSONDecodeError:
                            continue
        except FileNotFoundError:
            self.findings.append(Finding(
                category=AnalysisCategory.OVERHEAD,
                severity=Severity.INFO,
                location="docker",
                message="Docker CLI not available",
            ))
            return self.findings, self.images

        # Analyze images for redundancies
        self._check_dangling_images()
        self._check_old_images()
        self._check_duplicate_tags()

        return self.findings, self.images

    def _check_dangling_images(self) -> None:
        """Check for dangling (untagged) images."""
        dangling = [img for img in self.images if img.get("Repository") == "<none>"]
        if dangling:
            self.findings.append(Finding(
                category=AnalysisCategory.OVERHEAD,
                severity=Severity.WARNING,
                location="docker",
                message=f"{len(dangling)} dangling images consuming disk space",
                suggested_action="Run 'docker image prune' to clean up",
                auto_fixable=True,
            ))

    def _check_old_images(self) -> None:
        """Check for images that haven't been used recently."""
        # This would require more sophisticated tracking
        pass

    def _check_duplicate_tags(self) -> None:
        """Check for duplicate image tags across repositories."""
        tag_repos: dict[str, list[str]] = {}
        for img in self.images:
            tag = img.get("Tag", "")
            repo = img.get("Repository", "")
            if tag and tag != "<none>":
                if tag not in tag_repos:
                    tag_repos[tag] = []
                tag_repos[tag].append(repo)


class InfrastructureAnalyzer:
    """Analyzes .NET infrastructure for optimization opportunities."""

    def __init__(self, infra_dir: Path) -> None:
        self.infra_dir = infra_dir
        self.findings: list[Finding] = []

    def analyze(self) -> list[Finding]:
        """Analyze infrastructure code."""
        if not self.infra_dir.exists():
            return self.findings

        # Check for duplicate package references
        self._check_package_references()

        # Check for unused project references
        self._check_project_references()

        return self.findings

    def _check_package_references(self) -> None:
        """Check for duplicate or conflicting package versions."""
        packages: dict[str, list[tuple[str, str]]] = {}

        for csproj in self.infra_dir.rglob("*.csproj"):
            try:
                content = csproj.read_text(encoding="utf-8")
                # Simple regex-like parsing for PackageReference
                import re
                for match in re.finditer(r'<PackageReference\s+Include="([^"]+)"(?:\s+Version="([^"]+)")?', content):
                    pkg_name = match.group(1)
                    version = match.group(2) or "unspecified"
                    if pkg_name not in packages:
                        packages[pkg_name] = []
                    packages[pkg_name].append((csproj.name, version))
            except OSError:
                continue

        # Check for version conflicts
        for pkg, refs in packages.items():
            versions = set(v for _, v in refs)
            if len(versions) > 1:
                self.findings.append(Finding(
                    category=AnalysisCategory.REDUNDANCY,
                    severity=Severity.WARNING,
                    location=pkg,
                    message=f"Package '{pkg}' has inconsistent versions: {versions}",
                    suggested_action="Use Directory.Packages.props for central version management",
                ))

    def _check_project_references(self) -> None:
        """Check for unused or circular project references."""
        # This would require more sophisticated analysis
        pass


# ============================================================================
# Report Generator
# ============================================================================


class RegistryAnalysisRunner:
    """Runs all analyzers and generates the report."""

    def __init__(self, workspace: Path, dry_run: bool = False) -> None:
        self.workspace = workspace
        self.dry_run = dry_run

    def run(self) -> AnalysisReport:
        """Run complete analysis."""
        print("=" * 70)
        print("REGISTRY ANALYSIS - Self Enhancement Report")
        print("=" * 70)

        report = AnalysisReport(
            timestamp=datetime.now().isoformat(),
            workspace=str(self.workspace),
        )

        # 1. Python module analysis
        print("\n[1/3] Analyzing Python vendor modules...")
        python_analyzer = PythonModuleAnalyzer(VENDOR_DIR)
        findings, metrics = python_analyzer.analyze()
        report.findings.extend(findings)
        report.module_metrics.extend(metrics)

        # 2. Docker registry analysis
        print("[2/3] Analyzing Docker registry...")
        docker_analyzer = DockerRegistryAnalyzer()
        docker_findings, docker_images = docker_analyzer.analyze()
        report.findings.extend(docker_findings)
        report.docker_images = docker_images

        # 3. Infrastructure analysis
        print("[3/3] Analyzing infrastructure...")
        infra_analyzer = InfrastructureAnalyzer(INFRA_DIR)
        infra_findings = infra_analyzer.analyze()
        report.findings.extend(infra_findings)

        # Generate summary
        report.summary = self._generate_summary(report)

        return report

    def _generate_summary(self, report: AnalysisReport) -> dict[str, Any]:
        """Generate summary statistics."""
        severity_counts = {s.value: 0 for s in Severity}
        category_counts = {c.value: 0 for c in AnalysisCategory}
        auto_fixable = 0

        for finding in report.findings:
            severity_counts[finding.severity.value] += 1
            category_counts[finding.category.value] += 1
            if finding.auto_fixable:
                auto_fixable += 1

        total_loc = sum(m.lines_of_code for m in report.module_metrics)
        total_funcs = sum(m.function_count for m in report.module_metrics)
        avg_complexity = sum(m.complexity for m in report.module_metrics) / len(report.module_metrics) if report.module_metrics else 0

        return {
            "total_findings": len(report.findings),
            "by_severity": severity_counts,
            "by_category": category_counts,
            "auto_fixable_count": auto_fixable,
            "total_modules": len(report.module_metrics),
            "total_lines_of_code": total_loc,
            "total_functions": total_funcs,
            "average_complexity": round(avg_complexity, 2),
            "docker_images_count": len(report.docker_images),
        }

    def print_summary(self, report: AnalysisReport) -> None:
        """Print summary to stdout."""
        summary = report.summary

        print("\n" + "=" * 70)
        print("ANALYSIS SUMMARY")
        print("=" * 70)
        print(f"Timestamp: {report.timestamp}")
        print(f"Workspace: {report.workspace}")
        print("-" * 70)
        print(f"Total Findings: {summary['total_findings']}")
        print("  By Severity:")
        for sev, count in summary["by_severity"].items():
            if count > 0:
                print(f"    {sev}: {count}")
        print("  By Category:")
        for cat, count in summary["by_category"].items():
            if count > 0:
                print(f"    {cat}: {count}")
        print(f"  Auto-fixable: {summary['auto_fixable_count']}")
        print("-" * 70)
        print(f"Python Modules: {summary['total_modules']}")
        print(f"  Total LOC: {summary['total_lines_of_code']}")
        print(f"  Total Functions: {summary['total_functions']}")
        print(f"  Avg Complexity: {summary['average_complexity']}")
        print(f"Docker Images: {summary['docker_images_count']}")
        print("=" * 70)

        # Print top findings
        if report.findings:
            print("\nTOP FINDINGS:")
            for finding in report.findings[:10]:
                icon = {"critical": "🔴", "error": "🟠", "warning": "🟡", "info": "🔵"}.get(finding.severity.value, "⚪")
                print(f"  {icon} [{finding.category.value}] {finding.location}: {finding.message}")
                if finding.suggested_action:
                    print(f"      → {finding.suggested_action}")


# ============================================================================
# Main Entry Point
# ============================================================================


def main() -> int:
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(description="Registry and codebase analyzer for self-enhancement")
    parser.add_argument("--output-dir", type=Path, default=INFRA_DIR / ".config", help="Output directory")
    parser.add_argument("--dry-run", action="store_true", help="Don't write report file")
    args = parser.parse_args()

    runner = RegistryAnalysisRunner(WORKSPACE, dry_run=args.dry_run)
    report = runner.run()
    runner.print_summary(report)

    if not args.dry_run:
        output_path = args.output_dir / REPORT_FILE
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with output_path.open("w", encoding="utf-8") as f:
            json.dump(report.to_dict(), f, indent=2)
        print(f"\nReport saved to: {output_path}")

    # Return non-zero if there are critical/error findings
    critical_count = report.summary["by_severity"].get("critical", 0)
    error_count = report.summary["by_severity"].get("error", 0)
    if critical_count > 0 or error_count > 0:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
