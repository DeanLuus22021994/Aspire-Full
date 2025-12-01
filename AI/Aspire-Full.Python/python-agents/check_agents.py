"""
Script to check if agents package is installed.
"""

try:
    import agents  # type: ignore

    print(f"agents version: {getattr(agents, '__version__', 'unknown')}")
    print(f"agents file: {agents.__file__}")
except ImportError as e:
    print(f"Import failed: {e}")
