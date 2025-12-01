# GitHub Copilot Instructions

You are an expert developer working on the **Aspire-Full** solution, a distributed .NET 10 application using .NET Aspire.

## Architectural Guidelines

1.  **Project Structure**:
    - **Aspire-Full.Shared**: Contains all DTOs, Enums, and shared logic. **Always** define data contracts here.
    - **Aspire-Full.Api**: The backend API using EF Core and PostgreSQL.
    - **Aspire-Full.Web**: The React frontend.
    - **Aspire-Full.Tensor**: High-performance compute (C# + CUDA).
    - **Aspire-Full.Agents**: AI Agent orchestration.

2.  **Coding Standards**:
    - **Language**: Use C# 13 / .NET 10 features.
    - **Namespaces**: Use file-scoped namespaces (`namespace MyNamespace;`).
    - **Error Handling**: Prefer `Result<T>` pattern over throwing exceptions for business logic.
    - **Async**: Always use `async/await` and pass `CancellationToken`.
    - **Dependency Injection**: Use constructor injection. Avoid `IServiceLocator`.

3.  **Data Access**:
    - Use Entity Framework Core.
    - **No Raw SQL** unless absolutely necessary for performance (and documented).
    - Use Projections (`.Select()`) to fetch only needed data.

4.  **AI & Tensor Operations**:
    - When working in `Aspire-Full.Tensor`, remember it uses a hybrid Managed/Native architecture.
    - Use `GpuTensor<T>` for memory management.
    - Do not modify `Native/src/*.cpp` unless you are updating the CUDA kernels.

5.  **Testing**:
    - **Unit Tests**: xUnit. Mock external dependencies.
    - **E2E Tests**: NUnit with Playwright or Aspire Test Host.

## Behavior

- **Be Concise**: Provide code solutions directly.
- **Explain Context**: Briefly explain *why* a change is made if it affects architecture.
- **Safety**: Ensure all edits are safe and do not break the build.
