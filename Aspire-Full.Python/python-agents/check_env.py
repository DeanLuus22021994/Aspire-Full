import sys

try:
    import aspire_agents
    import openai
    import playwright.async_api
    import pydantic

    print("Imports successful")
except ImportError as e:
    print(f"Import failed: {e}")
    sys.exit(1)
