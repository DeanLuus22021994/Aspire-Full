import sys

try:
    import aspire_agents
    import openai
    import pydantic

    print(f"aspire_agents version: {getattr(aspire_agents, '__version__', 'unknown')}")
    print(f"openai version: {getattr(openai, '__version__', 'unknown')}")
    print(f"pydantic version: {getattr(pydantic, '__version__', 'unknown')}")

    # ...existing code...
    try:
        import opentelemetry
        import opentelemetry.exporter.otlp.proto.http
        import opentelemetry.sdk.trace

        print(
            f"opentelemetry version: {getattr(opentelemetry, '__version__', 'unknown')}"
        )
        print("OpenTelemetry imports successful")
    except ImportError:
        print("OpenTelemetry not found (optional)")

    print("Imports successful")
except ImportError as e:
    print(f"Import failed: {e}")
    sys.exit(1)
