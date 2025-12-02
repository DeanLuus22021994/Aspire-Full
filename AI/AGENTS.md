# Aspire-Full AI

Thin wrapper projects that provide CLI entry points and re-export types from the **Infra** layer.
All core implementations live in `Infra/` - this folder contains only CLI executables and backward-compatibility re-exports.

**Solution:** [Aspire-Full.AI.slnx](Aspire-Full.AI.slnx)

## Architecture: AI ↔ Infra Relationship

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              AI/ (This Folder)                          │
│  Thin wrappers: CLI entry points, global usings, backward compat       │
├─────────────────────────────────────────────────────────────────────────┤
│                                    ↓                                    │
├─────────────────────────────────────────────────────────────────────────┤
│                         Infra/ (Implementations)                        │
│  Aspire-Full.Agents.Core    → Agent orchestration, self-review          │
│  Aspire-Full.Connectors     → Embeddings, health, tracing, evaluation   │
│  Aspire-Full.Tensor.Core    → GPU runtime, memory pool, diagnostics     │
│  Aspire-Full.DevContainer   → Python defaults, devcontainer config      │
└─────────────────────────────────────────────────────────────────────────┘
```

## Projects

| Project | Type | Delegates To |
|---------|------|--------------|
| [Aspire-Full.Agents](Aspire-Full.Agents/) | CLI Exe | `Infra/Aspire-Full.Agents.Core` |
| [Aspire-Full.Embeddings](Aspire-Full.Embeddings/) | Re-export | `Infra/Aspire-Full.Connectors/Embeddings` |
| [Aspire-Full.Tensor](Aspire-Full.Tensor/) | Blazor | `Infra/Aspire-Full.Tensor.Core` + JSInterop |
| [Aspire-Full.Python](Aspire-Full.Python/) | Re-export | `Infra/Aspire-Full.DevContainer` + Python scripts |

## Implementation Locations

| Component | AI Location | Infra Implementation |
|-----------|-------------|----------------------|
| `EmbeddingService` | Re-export via global using | [Infra/Connectors/Embeddings/EmbeddingService.cs](../Infra/Aspire-Full.Connectors/Embeddings/EmbeddingService.cs) |
| `OnnxEmbeddingGenerator` | Re-export via global using | [Infra/Connectors/Embeddings/OnnxEmbeddingGenerator.cs](../Infra/Aspire-Full.Connectors/Embeddings/OnnxEmbeddingGenerator.cs) |
| `TensorDiagnostics` | Re-export via global using | [Infra/Tensor.Core/Diagnostics/TensorDiagnostics.cs](../Infra/Aspire-Full.Tensor.Core/Diagnostics/TensorDiagnostics.cs) |
| `PythonDefaults` | Re-export via global using | [Infra/DevContainer/Configuration/PythonDefaults.cs](../Infra/Aspire-Full.DevContainer/Configuration/PythonDefaults.cs) |
| Agent orchestration | `Program.cs` entry | [Infra/Agents.Core/](../Infra/Aspire-Full.Agents.Core/) |

## What Stays in AI/

Only these should remain:

1. **CLI Entry Points** (`Program.cs`) - executable bootstrapping
2. **Blazor JSInterop** (`TensorRuntimeService.cs`) - client-side GPU detection
3. **Global Usings** - backward compatibility re-exports
4. **Python Scripts** (`python-agents/`) - actual Python code
5. **Native Build Artifacts** (`build/*.dll`) - compiled CUDA binaries

## Coding Standards

- **Use Infra**: All new implementations go in `Infra/`, not here
- **Re-export Pattern**: Use `global using TypeAlias = Namespace.Type;`
- **Minimal Dependencies**: AI projects reference Infra projects, not vice versa

## Structure

```
AI/
├── Aspire-Full.AI.slnx               # AI solution (thin wrappers)
├── AGENTS.md                         # This file
├── Aspire-Full.Agents/
│   ├── Program.cs                    # CLI entry → Agents.Core
│   └── [Global usings only]
├── Aspire-Full.Embeddings/
│   ├── *.cs                          # Global using re-exports
│   └── → Infra/Connectors/Embeddings
├── Aspire-Full.Tensor/
│   ├── GlobalUsings.cs               # Re-exports from Tensor.Core
│   ├── TensorRuntimeService.cs       # JSInterop (stays here)
│   └── Native/                       # CUDA sources (build only)
└── Aspire-Full.Python/
    ├── PythonDefaults.cs             # Re-export from DevContainer
    └── python-agents/                # Actual Python code
```
