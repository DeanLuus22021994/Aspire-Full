# Aspire-Full Infrastructure

All infrastructure components are now properly encapsulated in the `Infra/` folder.

**Solution:** [Aspire-Full.Infra.slnx](Aspire-Full.Infra.slnx)

## Projects

| Project | Description |
|---------|-------------|
| [Aspire-Full](Aspire-Full/Aspire-Full.csproj) | AppHost orchestrator - distributed application entry point |
| [Aspire-Full.Connectors](Aspire-Full.Connectors/Aspire-Full.Connectors.csproj) | Connector hub, health monitoring, tracing, evaluation |
| [Aspire-Full.DevContainer](Aspire-Full.DevContainer/Aspire-Full.DevContainer.csproj) | Devcontainer configuration and defaults |
| [Aspire-Full.DockerRegistry](Aspire-Full.DockerRegistry/Aspire-Full.DockerRegistry.csproj) | Docker build, registry management, garbage collection |
| [Aspire-Full.Infra.Tests](Aspire-Full.Infra.Tests/Aspire-Full.Infra.Tests.csproj) | Infrastructure unit tests |
| [Aspire-Full.ServiceDefaults](Aspire-Full.ServiceDefaults/Aspire-Full.ServiceDefaults.csproj) | Health checks, telemetry, service discovery |
| [Aspire-Full.Tensor.Core](Aspire-Full.Tensor.Core/Aspire-Full.Tensor.Core.csproj) | GPU runtime core, memory pooling, native interop |
| [Aspire-Full.VectorStore](Aspire-Full.VectorStore/Aspire-Full.VectorStore.csproj) | Qdrant vector database integration |

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
