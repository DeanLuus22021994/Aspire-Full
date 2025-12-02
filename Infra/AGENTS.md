# Aspire-Full Infrastructure

All infrastructure components are now properly encapsulated in the `Infra/` folder.

**Solution:** [Aspire-Full.Infra.slnx](Aspire-Full.Infra.slnx)

## Projects

| Project | Description |
|---------|-------------|
| [Aspire-Full](Aspire-Full/Aspire-Full.csproj) | AppHost orchestrator - distributed application entry point |
| [Aspire-Full.Agents.Core](Aspire-Full.Agents.Core/Aspire-Full.Agents.Core.csproj) | Agent abstractions, self-enhancement automation |
| [Aspire-Full.Connectors](Aspire-Full.Connectors/Aspire-Full.Connectors.csproj) | Connector hub, health monitoring, tracing, evaluation |
| [Aspire-Full.DevContainer](Aspire-Full.DevContainer/Aspire-Full.DevContainer.csproj) | Devcontainer configuration and defaults |
| [Aspire-Full.DockerRegistry](Aspire-Full.DockerRegistry/Aspire-Full.DockerRegistry.csproj) | Docker build, registry management, garbage collection |
| [Aspire-Full.Infra.Tests](Aspire-Full.Infra.Tests/Aspire-Full.Infra.Tests.csproj) | Infrastructure unit tests |
| [Aspire-Full.ServiceDefaults](Aspire-Full.ServiceDefaults/Aspire-Full.ServiceDefaults.csproj) | Health checks, telemetry, service discovery |
| [Aspire-Full.Tensor.Core](Aspire-Full.Tensor.Core/Aspire-Full.Tensor.Core.csproj) | GPU runtime core, memory pooling, native interop |
| [Aspire-Full.VectorStore](Aspire-Full.VectorStore/Aspire-Full.VectorStore.csproj) | Qdrant vector database integration |

## Self-Enhancement Automation

The infrastructure supports agent self-enhancement through automated analysis and optimization:

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `SelfEnhancementService` | [Agents.Core/Maintenance](Aspire-Full.Agents.Core/Maintenance/SelfEnhancementService.cs) | Orchestrates analyze → fix → verify cycles |
| `MaintenanceAgent` | [Agents.Core/Maintenance](Aspire-Full.Agents.Core/Maintenance/MaintenanceAgent.cs) | GPU-accelerated Docker maintenance tasks |
| `RedundancyDetectionPolicy` | [DockerRegistry/GarbageCollection](Aspire-Full.DockerRegistry/GarbageCollection/RedundancyDetectionPolicy.cs) | Identifies redundant Docker images |
| `registry_analyzer.py` | [DevContainer/Scripts](Aspire-Full.DevContainer/Scripts/registry_analyzer.py) | Python/Docker/Infra analysis tool |

### Analysis Reports

Reports are generated to `.config/registry-analysis.json` and include:
- Python vendor module metrics (LOC, complexity, imports)
- Docker image redundancy analysis (dangling, superseded)
- .NET package reference consistency checks

### Running Analysis

```bash
# Dry run (no file output)
python Infra/Aspire-Full.DevContainer/Scripts/registry_analyzer.py --dry-run

# Full analysis with report
python Infra/Aspire-Full.DevContainer/Scripts/registry_analyzer.py
```

### Automation Triggers

The analysis runs automatically during:
1. DevContainer post-create hook
2. `ISelfEnhancementService.RunFullCycleAsync()` invocation
3. Manual execution via scripts

## Configuration

All configuration files are centralized in [.config/](.config/):

| File | Purpose |
|------|---------|
| [aspire-config.yaml](.config/aspire-config.yaml) | Unified configuration (preferred) |
| [environment.yaml](.config/environment.yaml) | Environment-specific settings |
| [config.yaml](.config/config.yaml) | Legacy runtime configuration |

## Structure

```
Infra/
├── .config/                          # Configuration files
├── Aspire-Full/                      # AppHost orchestrator
│   └── Configuration/                # ConfigLoader, ConfigModels
├── Aspire-Full.Connectors/           # Connector infrastructure
│   ├── Connectors/                   # VectorStore, Tensor bridges
│   ├── Evaluation/                   # Evaluation orchestrator
│   ├── Health/                       # Connector health registry
│   └── Tracing/                      # OpenTelemetry integration
├── Aspire-Full.DevContainer/         # Devcontainer defaults
├── Aspire-Full.DockerRegistry/       # Docker/registry management
│   ├── docker/                       # Dockerfiles, bake configs
│   ├── GarbageCollection/            # Registry cleanup policies
│   └── Services/                     # Registry client, workers
├── Aspire-Full.Infra.Tests/          # Infrastructure tests
│   └── Connectors/                   # Connector unit tests
├── Aspire-Full.ServiceDefaults/      # Aspire service defaults
│   └── Health/                       # GPU health checks
├── Aspire-Full.Tensor.Core/          # GPU compute core
│   ├── Abstractions/                 # ITensorRuntime, IGpuResourceMonitor
│   ├── Memory/                       # GpuMemoryPool, buffer management
│   ├── Models/                       # ModelRegistry, eviction policies
│   ├── Native/                       # P/Invoke, NativeTensorContext
│   └── Orchestration/                # Job store, orchestration extensions
└── Aspire-Full.VectorStore/          # Qdrant integration
    └── Extensions/                   # DI extensions
```

## Migration Summary

| Component | Previous Location | Current Location |
|-----------|-------------------|------------------|
| VectorStore | `AI/Aspire-Full.VectorStore/` | [Aspire-Full.VectorStore](Aspire-Full.VectorStore/) |
| Connectors | `Core/Aspire-Full.Connectors/` | [Aspire-Full.Connectors](Aspire-Full.Connectors/) |
| Configuration | `.config/` | [.config](.config/) |
| TensorOrchestration | `AI/Aspire-Full.Tensor/Services/` | [Tensor.Core/Orchestration](Aspire-Full.Tensor.Core/Orchestration/) |

## Key Files

- **ConfigLoader:** [Aspire-Full/Configuration/ConfigLoader.cs](Aspire-Full/Configuration/ConfigLoader.cs)
- **GPU Health Check:** [ServiceDefaults/Health/GpuHealthCheck.cs](Aspire-Full.ServiceDefaults/Health/GpuHealthCheck.cs)
- **Model Registry:** [Tensor.Core/Models/ModelRegistry.cs](Aspire-Full.Tensor.Core/Models/ModelRegistry.cs)
- **Tensor Runtime:** [Tensor.Core/TensorRuntime.cs](Aspire-Full.Tensor.Core/TensorRuntime.cs)
