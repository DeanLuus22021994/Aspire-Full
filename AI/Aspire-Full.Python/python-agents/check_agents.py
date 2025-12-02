"""Check if agents and aspire_agents packages are installed.

Validates:
- OpenAI Agents SDK installation
- Aspire Agents package with TensorCore support
- Sub-Agent orchestration capabilities

Environment Variables (from Dockerfile):
- ASPIRE_SUBAGENT_MAX_CONCURRENT: Max concurrent sub-agents
- ASPIRE_AGENT_THREAD_POOL_SIZE: Thread pool size
"""

import sys
from pathlib import Path

# Add src to path for local development
sys.path.append(str(Path(__file__).parent / "src"))

print("=== OpenAI Agents SDK ===")
try:
    import agents  # type: ignore

    print(f"  version: {getattr(agents, '__version__', 'unknown')}")
    print(f"  location: {agents.__file__}")
except ImportError as e:
    print(f"  ❌ Import failed: {e}")

print("\n=== Aspire Agents ===")
try:
    import aspire_agents  # type: ignore

    print(f"  version: {getattr(aspire_agents, '__version__', 'unknown')}")

    # Check SubAgent orchestration
    from aspire_agents import (
        ASPIRE_AGENT_THREAD_POOL_SIZE,
        ASPIRE_SUBAGENT_MAX_CONCURRENT,
        SubAgentConfig,
    )

    print(f"  thread_pool_size: {ASPIRE_AGENT_THREAD_POOL_SIZE}")
    print(f"  max_concurrent: {ASPIRE_SUBAGENT_MAX_CONCURRENT}")
    print("  ✅ SubAgent orchestration available")
except ImportError as e:
    print(f"  ❌ Import failed: {e}")

print("\n=== GPU Status ===")
try:
    from aspire_agents import ASPIRE_COMPUTE_MODE, ensure_tensor_core_gpu

    print(f"  compute_mode: {ASPIRE_COMPUTE_MODE}")
    if ASPIRE_COMPUTE_MODE in ("gpu", "hybrid"):
        try:
            info = ensure_tensor_core_gpu()
            print(f"  gpu: {info.name}")
            print(f"  compute_capability: {info.compute_capability}")
            print(f"  tensor_alignment: {info.tensor_alignment}")
            print("  ✅ Tensor Core GPU ready")
        except Exception as e:
            print(f"  ⚠️ GPU unavailable: {e}")
    else:
        print("  ℹ️ CPU-only mode")
except ImportError as e:
    print(f"  ❌ Import failed: {e}")
