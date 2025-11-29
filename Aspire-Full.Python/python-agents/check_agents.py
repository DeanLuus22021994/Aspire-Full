import sys

try:
    import agents

    print(f"agents version: {getattr(agents, '__version__', 'unknown')}")
    print(f"agents file: {agents.__file__}")
except ImportError as e:
    print(f"Import failed: {e}")
