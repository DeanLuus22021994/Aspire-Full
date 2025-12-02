# Aspire-Full AI

AI/ML components for GPU-accelerated inference, embeddings, and agent orchestration.

**Solution:** [Aspire-Full.AI.slnx](Aspire-Full.AI.slnx)

## Projects

| Project | Description |
|---------|-------------|
| [Aspire-Full.Agents](Aspire-Full.Agents/Aspire-Full.Agents.csproj) | Subagent orchestration, self-review, maintenance automation |
| [Aspire-Full.Embeddings](Aspire-Full.Embeddings/Aspire-Full.Embeddings.csproj) | ONNX embedding generation with GPU acceleration |
| [Aspire-Full.Tensor](Aspire-Full.Tensor/Aspire-Full.Tensor.csproj) | WebGPU/CUDA tensor operations for Blazor clients |
| [Aspire-Full.Python](Aspire-Full.Python/Aspire-Full.Python.csproj) | Python agent integration via uv/pyproject.toml |

## Shared Dependencies

| Reference | Purpose |
|-----------|---------|
| [Core/Aspire-Full.Shared](../Core/Aspire-Full.Shared/) | DTOs, Result<T>, agent abstractions |
| [Infra/Aspire-Full.VectorStore](../Infra/Aspire-Full.VectorStore/) | Qdrant vector store integration |
| [Infra/Aspire-Full.Tensor.Core](../Infra/Aspire-Full.Tensor.Core/) | GPU runtime, memory pooling |

## AI Agent Architecture

### Abstractions (in `Core/Aspire-Full.Shared/Abstractions/`)

| Interface | Purpose |
|-----------|---------|
| `ISubagentSelfReviewService` | Creates retrospectives and delegation plans from agent updates |
| `IMaintenanceAgent` | GPU-accelerated workspace maintenance automation |
| `SubagentUpdate` | Normalized input for agent self-review processing |

### Implementations (in `AI/Aspire-Full.Agents/`)

| Class | Implements | Purpose |
|-------|------------|---------|
| `SubagentSelfReviewService` | `ISubagentSelfReviewService` | Generates structured retrospectives with TimeProvider |
| `MaintenanceAgent` | `IMaintenanceAgent` | Docker-based maintenance with Result<T> error handling |
| `SubagentCatalog` | - | Static registry of subagent definitions |

## Coding Standards

- **TimeProvider**: All time operations use `TimeProvider` (not `DateTime.UtcNow`)
- **Result<T>**: Service methods return `Result<T>` instead of throwing exceptions
- **ILogger**: All logging via `ILogger<T>` (not `Console.WriteLine`)
- **DI-Ready**: All services are interface-backed for testability

## Structure

```
AI/
├── .pycodestyle                      # Python linting config
├── pyrightconfig.json                # Python type checking
├── pytest.ini                        # Python test config
├── Aspire-Full.AI.slnx               # AI solution file
├── AGENTS.md                         # This file
├── Aspire-Full.Agents/               # C# agent orchestration
│   ├── MaintenanceAgent.cs           # IMaintenanceAgent implementation
│   ├── SubagentSelfReviewService.cs  # ISubagentSelfReviewService implementation
│   ├── SubagentCatalog.cs            # Static agent definitions
│   └── Program.cs                    # CLI entry point
├── Aspire-Full.Embeddings/           # ONNX embedding service
│   ├── EmbeddingService.cs           # GPU-accelerated embeddings
│   └── OnnxEmbeddingGenerator.cs     # ONNX runtime wrapper
├── Aspire-Full.Tensor/               # Blazor WebGPU/CUDA
│   ├── TensorRuntimeService.cs       # Client-side tensor detection
│   ├── TensorModelDescriptor.cs      # Model catalog
│   ├── Services/                     # Job coordination, compute
│   └── Diagnostics/                  # ActivitySource tracing
└── Aspire-Full.Python/               # Python agent integration
    └── python-agents/                # uv-managed Python project
        └── pyproject.toml            # Python dependencies
```

## Key Patterns

### Result<T> Pattern
```csharp
// Service methods return Result<T> for graceful error handling
public async Task<Result<MaintenanceResult>> RunAsync(string workspace, CancellationToken ct)
{
    var buildResult = await RunDockerAsync(buildArgs, workspace, ct);
    if (!buildResult.IsSuccess)
        return Result<MaintenanceResult>.Failure($"Build failed: {buildResult.Error}");
    
    return Result<MaintenanceResult>.Success(new MaintenanceResult { ... });
}
```

### TimeProvider Injection
```csharp
// Use TimeProvider for testable time operations
public SubagentSelfReviewService(TimeProvider timeProvider)
{
    _timeProvider = timeProvider;
}

public SubagentRetrospective CreateRetrospective(SubagentUpdate update)
{
    return new SubagentRetrospective(update.Role, ..., _timeProvider.GetUtcNow());
}
```

### Interface-Backed Services
```csharp
// All services implement interfaces for DI/testing
ISubagentSelfReviewService service = new SubagentSelfReviewService(timeProvider);
IMaintenanceAgent agent = new MaintenanceAgent(logger, timeProvider);
```
