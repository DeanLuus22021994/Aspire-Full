"""Check environment for required packages and ASPIRE_AGENT_* configuration.

Validates:
- Required Python packages (openai, pydantic, aspire_agents)
- GPU Tensor Core availability
- Environment variables from Dockerfile configuration
- OpenTelemetry tracing (optional)

Environment Variables Checked:
- ASPIRE_AGENT_THREAD_POOL_SIZE: Thread pool size (default: 8)
- ASPIRE_SUBAGENT_MAX_CONCURRENT: Max concurrent sub-agents (default: 16)
- ASPIRE_TENSOR_BATCH_SIZE: Batch size for tensor ops (default: 32)
- ASPIRE_TENSOR_OFFLOAD_ENABLED: Enable tensor offloading (default: 1)
- ASPIRE_SUBAGENT_GPU_SHARE: Enable GPU sharing (default: 1)
- ASPIRE_COMPUTE_MODE: Compute mode - gpu|cpu|hybrid (default: gpu)
- CUDA_TENSOR_CORE_ALIGNMENT: Memory alignment in bytes (default: 128)
"""

import os
import sys
from pathlib import Path

# Add src to path so we can import aspire_agents
sys.path.append(str(Path(__file__).parent / "src"))


def check_aspire_env_vars() -> None:
    """Check ASPIRE_AGENT_* environment variables."""
    print("\n=== ASPIRE Agent Environment Variables ===")
    env_vars = [
        ("ASPIRE_AGENT_THREAD_POOL_SIZE", "8"),
        ("ASPIRE_SUBAGENT_MAX_CONCURRENT", "16"),
        ("ASPIRE_TENSOR_BATCH_SIZE", "32"),
        ("ASPIRE_TENSOR_OFFLOAD_ENABLED", "1"),
        ("ASPIRE_SUBAGENT_GPU_SHARE", "1"),
        ("ASPIRE_COMPUTE_MODE", "gpu"),
        ("CUDA_TENSOR_CORE_ALIGNMENT", "128"),
        ("PYTHON_GIL", "0"),
        ("PYTHON_JIT", "1"),
    ]
    for var, default in env_vars:
        value = os.environ.get(var, f"(default: {default})")
        print(f"  {var}: {value}")


def check_gpu() -> None:
    """Check GPU availability and Tensor Core support."""
    print("\n=== GPU / Tensor Core Status ===")
    try:
        import torch

        if torch.cuda.is_available():
            props = torch.cuda.get_device_properties(0)
            major, minor = torch.cuda.get_device_capability(0)
            print(f"  GPU: {props.name}")
            print(f"  Compute Capability: {major}.{minor}")
            print(f"  Memory: {props.total_memory / (1024**3):.2f} GB")
            print(f"  Tensor Cores: {'Yes' if major >= 7 else 'No'}")
        else:
            print("  CUDA not available")
    except ImportError:
        print("  torch not installed")


try:
    import aspire_agents  # type: ignore
    import openai  # type: ignore
    import pydantic

    print("=== Package Versions ===")
    print(f"  aspire_agents: {getattr(aspire_agents, '__version__', 'unknown')}")
    print(f"  openai: {getattr(openai, '__version__', 'unknown')}")
    print(f"  pydantic: {getattr(pydantic, '__version__', 'unknown')}")

    # Check Python version and GIL status
    print(f"\n=== Python Runtime ===")
    print(f"  Version: {sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}")
    if hasattr(sys, "_is_gil_enabled"):
        print(f"  GIL: {'enabled' if sys._is_gil_enabled() else 'disabled (free-threaded)'}")
    else:
        print("  GIL: enabled (pre-3.15)")

    check_aspire_env_vars()
    check_gpu()

    # Check OpenTelemetry
    print("\n=== OpenTelemetry Status ===")
    try:
        import opentelemetry  # type: ignore
        import opentelemetry.exporter.otlp.proto.http  # type: ignore
        import opentelemetry.sdk.trace  # type: ignore

        print(f"  version: {getattr(opentelemetry, '__version__', 'unknown')}")
        print("  status: available")
    except ImportError:
        print("  status: not installed (optional)")

    print("\n✅ Environment check passed")
except ImportError as e:
    print(f"❌ Import failed: {e}")
    sys.exit(1)
