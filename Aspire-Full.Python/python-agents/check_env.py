import sys

try:
    import aspire_agents
    import openai
    import pydantic

    print("Imports successful")
except ImportError as e:
    print(f"Import failed: {e}")
    sys.exit(1)
