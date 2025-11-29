try:
    from aspire_agents.gpu import ensure_tensor_core_gpu

    print("aspire_agents.gpu imported successfully")
except ImportError as e:
    print(f"Import failed: {e}")
