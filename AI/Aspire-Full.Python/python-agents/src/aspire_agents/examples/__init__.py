"""Aspire Agents Examples - Demonstrations of Agent & TensorCore patterns.

This package contains examples demonstrating:
- Agent orchestration with TensorCore compute
- Sub-Agent parallelization with GPU memory sharing
- Semantic guardrails with GPU-accelerated similarity
- Python 3.15+ free-threaded patterns (PYTHON_GIL=0)

Example Categories:
- agent_patterns/: Advanced agent orchestration patterns
- basic/: Simple agent demonstrations
- customer_service/: Multi-agent customer service example
- financial_research_agent/: Financial analysis with tools
- handoffs/: Agent handoff patterns
- hosted_mcp/: Model Context Protocol examples
- mcp/: MCP tool integration
- memory/: Agent memory patterns
- model_providers/: Provider-specific examples
- realtime/: Real-time agent streaming
- reasoning_content/: Reasoning and analysis patterns
- research_bot/: Research automation example
- tools/: Tool integration examples
- voice/: Voice agent examples

Environment Variables (from Dockerfile):
- ASPIRE_AGENT_THREAD_POOL_SIZE: Thread pool size (default: 8)
- ASPIRE_SUBAGENT_MAX_CONCURRENT: Max concurrent sub-agents (default: 16)
- ASPIRE_TENSOR_BATCH_SIZE: Batch size for tensor ops (default: 32)
- ASPIRE_COMPUTE_MODE: Compute mode - gpu|cpu|hybrid (default: gpu)
- CUDA_TENSOR_CORE_ALIGNMENT: Memory alignment in bytes (default: 128)
"""

from typing import Final

__all__: Final[tuple[str, ...]] = ()
