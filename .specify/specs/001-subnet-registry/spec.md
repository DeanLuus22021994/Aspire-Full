# Spec 001: High Performance Subnet & Internal Docker Registry

## Background
The Aspire Agents require a high-performance, low-latency environment with instant readiness. Current runtime dependency installation (pip install, model downloads) creates unacceptable latency.

## Requirements
1. **Internal Docker Registry**: A local registry service running on port 5000, accessible to the Aspire orchestration.
2. **High Performance Subnet**: A dedicated Docker network (`aspire-network`) optimized for container-to-container communication.
3. **Pre-Provisioned Agents**:
    - Python 3.14 (Free-Threading enabled).
    - All dependencies (`torch`, `transformers`, `playwright`, `sounddevice`) pre-installed.
    - Models (`sentence-transformers/all-MiniLM-L6-v2`) pre-downloaded.
4. **Instant Readiness**: Agent containers must start and be ready to serve requests immediately (<1s startup time excluding model load, <5s including model load).
5. **Zero External Dependency at Runtime**: No internet access required for agent startup (no pip, no huggingface hub downloads).

## Constraints
- Must integrate with .NET Aspire `AppHost`.
- Must support NVIDIA Tensor Cores.
