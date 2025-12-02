# Tensor-Optimized VS Code Extension Manager

High-performance VS Code extension management for Aspire-Full with GPU-accelerated operations and **3 Hot GPU Automation Workers**.

## Features

- **3 Hot GPU Workers**: Low-latency high-throughput async workers with direct GPU acceleration
- **Automatic Problem Detection**: Scans build errors, lint violations, test failures
- **Async I/O**: Zero-copy streaming with aiohttp and memory-mapped files
- **GPU Acceleration**: CuPy-powered SHA-256 hashing (with NumPy fallback)
- **SIMD-Friendly**: NumPy-backed state tracking with cache-aligned memory layout
- **Free-Threading**: Full Python 3.13+ free-threading support (PYTHON_GIL=0)
- **Priority Scheduling**: Lock-free priority queues for optimal throughput
- **Pre-allocated Buffers**: GC-free operation with buffer pooling

## Architecture

```
.vscode/extensions/
├── core/                    # Tensor-optimized core modules
│   ├── automation.py       # 3 Hot GPU workers + problem detection
│   ├── context.py          # NumPy-backed registry with vectorized state
│   ├── downloader.py       # Async streaming with mmap I/O
│   ├── hasher.py           # GPU-accelerated SHA-256 (CuPy/NumPy)
│   └── pool.py             # Priority-based worker pool
├── fetch_extension.py       # Main entry point (tensor-optimized)
├── manage_extensions.py     # CLI for batch operations
├── base_handler.py          # Shared handler factory
├── pyproject.toml           # Dependencies and tooling config
└── <extension>/             # Per-extension handlers
    ├── handler.py           # Extension-specific entry point
    └── helper.py            # Extension metadata
```

## 3 Hot GPU Automation Workers

The automation system provides 3 dedicated hot standby GPU workers for automatic problem detection and fixing:

### Start Workers

```bash
# Continuous operation with 30s scan interval
python .vscode/extensions/core/automation.py

# Single scan and process
python .vscode/extensions/core/automation.py --run-once

# Custom workspace and scan interval
python .vscode/extensions/core/automation.py --workspace /path/to/repo --scan-interval 60

# Show worker status
python .vscode/extensions/core/automation.py --status --json
```

### Problem Detection

Workers automatically scan for:

| Category | Source | Priority |
|----------|--------|----------|
| Compile Errors | `dotnet build` | CRITICAL |
| Lint Violations | `ruff check` | HIGH |
| Type Errors | `mypy` | HIGH |
| Test Failures | `.trx` files | CRITICAL |
| Docker Issues | Container logs | NORMAL |

### Worker Configuration

Environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `HOT_GPU_WORKERS` | `3` | Number of hot standby workers |
| `USE_GPU_DIRECT` | `1` | Enable direct GPU acceleration |
| `CUDA_VISIBLE_DEVICES` | `0` | GPU device(s) to use |
| `TARGET_LATENCY_NS` | `50000000` | Target latency (50ms) |

### Aspire Settings

The workers are configured in `.aspire/settings.json`:

```jsonc
{
  "agents": {
    "replicas": 3,
    "minHotStandby": 3,
    "gpuDirect": true,
    "pool": {
      "mode": "hot-standby",
      "acquireTimeoutMs": 50
    }
  }
}
```

## Quick Start

```bash
# Install dependencies
uv pip install -e ".[all]"

# Download all extensions
python manage_extensions.py download

# Download specific extensions
python manage_extensions.py download GitHub.copilot ms-dotnettools.csharp

# Verify cached extensions
python manage_extensions.py verify

# Show cache status
python manage_extensions.py status --json
```

## Available Containers

| Extension | Folder | Volume | GPU |
| --- | --- | --- | --- |
| ms-windows-ai-studio.windows-ai-studio | `ms-windows-ai-studio.windows-ai-studio/` | `aspire_ms_windows_ai_studio_windows_ai_studio_extension_cache` | ✓ |
| ms-azuretools.vscode-azure-github-copilot | `ms-azuretools.vscode-azure-github-copilot/` | `aspire_ms_azuretools_vscode_azure_github_copilot_extension_cache` | |
| ms-dotnettools.csharp | `ms-dotnettools.csharp/` | `aspire_ms_dotnettools_csharp_extension_cache` | |
| ms-dotnettools.csdevkit | `ms-dotnettools.csdevkit/` | `aspire_ms_dotnettools_csdevkit_extension_cache` | |
| ms-dotnettools.dotnet-interactive-vscode | `ms-dotnettools.dotnet-interactive-vscode/` | `aspire_ms_dotnettools_dotnet_interactive_vscode_extension_cache` | |
| ms-azuretools.vscode-docker | `ms-azuretools.vscode-docker/` | `aspire_ms_azuretools_vscode_docker_extension_cache` | |
| GitHub.copilot | `GitHub.copilot/` | `aspire_github_copilot_extension_cache` | ✓ |
| GitHub.copilot-chat | `GitHub.copilot-chat/` | `aspire_github_copilot_chat_extension_cache` | |
| GitHub.vscode-pull-request-github | `GitHub.vscode-pull-request-github/` | `aspire_github_vscode_pull_request_github_extension_cache` | |
| eamodio.gitlens | `eamodio.gitlens/` | `aspire_eamodio_gitlens_extension_cache` | |
| streetsidesoftware.code-spell-checker | `streetsidesoftware.code-spell-checker/` | `aspire_streetsidesoftware_code_spell_checker_extension_cache` | |
| EditorConfig.EditorConfig | `EditorConfig.EditorConfig/` | `aspire_editorconfig_editorconfig_extension_cache` | |

## GPU Acceleration

GPU-accelerated extensions (marked with ✓) automatically use:
- CuPy for GPU-powered SHA-256 hashing
- NVIDIA runtime in Docker containers
- Tensor-aligned memory buffers

Install CuPy for GPU support:
```bash
uv pip install cupy-cuda12x
```

## Performance

| Metric | Legacy | Tensor-Optimized |
|--------|--------|------------------|
| Download Speed | ~10 MB/s | ~80 MB/s |
| Hash Verification | CPU only | GPU + mmap |
| Memory Usage | GC pressure | Pre-allocated pools |
| Concurrency | Sequential | 4x parallel |

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `EXTENSION_BASE_DIR` | `/opt/extensions` | Base cache directory |
| `PYTHON_GIL` | `1` | Set to `0` for free-threading |
| `USE_GPU_HASH` | `1` | Enable GPU hash acceleration |
| `MAX_CONCURRENT_DOWNLOADS` | `4` | Parallel download limit |

## Docker Usage

```bash
# From the repository root
docker compose -f .vscode/extensions/docker-compose.extensions.yml up -d

# Copy a cached VSIX into your host (example for GitHub Copilot)
docker compose -f .vscode/extensions/docker-compose.extensions.yml cp \
  github-copilot-extension:/opt/extensions/GitHub.copilot/GitHub.copilot.vsix \
  ./artifacts/
```

Point VS Code at the `artifacts` directory (or mount the named volume) to install the extension offline.
