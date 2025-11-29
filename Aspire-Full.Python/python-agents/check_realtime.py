try:
    import agents.realtime

    print("agents.realtime imported successfully")
    print(dir(agents.realtime))
except ImportError as e:
    print(f"Import failed: {e}")
