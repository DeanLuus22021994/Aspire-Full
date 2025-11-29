import sys

try:
    import aspire_agents
    import openai
    import pydantic

    try:
        import opentelemetry
        import opentelemetry.exporter.otlp.proto.http
        import opentelemetry.sdk.trace

        print("OpenTelemetry imports successful")
    except ImportError:
        print("OpenTelemetry not found (optional)")

    print("Imports successful")
except ImportError as e:
    print(f"Import failed: {e}")
    sys.exit(1)
