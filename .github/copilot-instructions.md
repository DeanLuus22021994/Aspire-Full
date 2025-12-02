# GitHub Copilot Instructions

You are an expert developer working on the **Aspire-Full** solution, a distributed .NET 10 application using .NET Aspire 9.3.

## Solution Structure

The codebase is organized into four main areas:

### Core (`/Core`)
- **Aspire-Full.Shared**: DTOs, Enums, shared logic, and `ISecurePathService` for jail-protected file access
- **Aspire-Full.Github**: GitHub MCP server integration
- **Aspire-Full.Pipeline**: CLI pipeline tooling

### Infra (`/Infra`)
- **Aspire-Full** (AppHost): Aspire orchestration entry point
- **Aspire-Full.Tensor.Core**: GPU tensor operations, `IComputeModeService` for GPU/CPU/Hybrid mode toggling
- **Aspire-Full.Agents.Core**: AI agent abstractions and orchestration
- **Aspire-Full.Connectors**: ONNX, gRPC, and AI service connectors
- **Aspire-Full.VectorStore**: Qdrant vector database integration
- **Aspire-Full.ServiceDefaults**: OpenTelemetry, health checks, resilience patterns
- **Aspire-Full.DockerRegistry**: Container image management with NVIDIA BuildKit

### AI (`/AI`) - Thin Wrappers
- **Aspire-Full.Tensor**: Delegates to `Infra/Aspire-Full.Tensor.Core`
- **Aspire-Full.Embeddings**: Delegates to `Infra/Aspire-Full.Connectors`
- **Aspire-Full.Agents**: Delegates to `Infra/Aspire-Full.Agents.Core`
- **Aspire-Full.Python**: Python agent runtime with uv/ruff toolchain

### Web (`/Web`)
- **Aspire-Full.Api**: Backend API (EF Core + PostgreSQL)
- **Aspire-Full.Gateway**: API Gateway with service discovery
- **Aspire-Full.Web**: React frontend
- **Aspire-Full.WebAssembly**: Blazor WASM client

## Coding Standards

1. **Language**: C# 14 / .NET 10 with file-scoped namespaces
2. **Error Handling**: **MANDATORY** Use `Result<T>` pattern. No exceptions for business logic.
3. **Async**: Always use `async/await` with `CancellationToken`
4. **Time**: **MANDATORY** Use `TimeProvider` instead of `DateTime.Now/UtcNow`
5. **Logging**: **MANDATORY** Use `ILogger<T>` instead of `Console.WriteLine`
6. **DI**: Constructor injection only. No service locator pattern.
7. **Packages**: **MANDATORY** Use Central Package Management (`Directory.Packages.props`)

## GPU Compute Architecture

1. **Compute Mode Service**: Use `IComputeModeService` to toggle GPU/CPU/Hybrid modes at runtime
   - `ComputeMode.Gpu`: Full GPU offload, zero CPU-bound compute
   - `ComputeMode.Cpu`: Fallback when GPU unavailable
   - `ComputeMode.Hybrid`: Dynamic routing based on operation type
   
2. **Offload Strategy**: Configure via `OffloadStrategy` enum
   - `Full`: All tensor/embedding operations go to compute service
   - `Selective`: Only heavy operations (MatMul, Convolution) offload
   - `Local`: GPU acceleration without remote offload

3. **TensorRuntime**: Always check `ShouldUseGpu(OperationType)` before operations

## Shared Storage Architecture

**SINGLE HOST MOUNT**: All persistent data uses `C:\SHARED` (host) → `/shared` (container)

1. Use `ISecurePathService` for jail-protected file access
2. Subdirectories: `models/`, `qdrant/`, `cache/`, `logs/`, `tmp/`
3. Path traversal attacks are blocked by `SecurePathService`
4. All Docker containers bind mount the same shared path

## Data Access

- Use Entity Framework Core with projections (`.Select()`)
- No raw SQL unless documented for performance
- PostgreSQL for relational data, Qdrant for vectors

## Testing

- **Unit Tests**: xUnit with FluentAssertions, Moq
- **E2E Tests**: NUnit with Playwright or Aspire Test Host
- Run with `dotnet test Tests/Aspire-Full.Tests.Unit`

## Configuration

- Settings: `.aspire/settings.json` (Aspire 8.0 schema)
- Unified config: `Infra/.config/aspire-config.yaml`
- Docker: `.devcontainer/docker-compose.yml`

## Behavior

- **Be Concise**: Provide code solutions directly
- **Explain Context**: Briefly explain architectural impacts
- **Safety**: Ensure all edits compile and don't break the build
