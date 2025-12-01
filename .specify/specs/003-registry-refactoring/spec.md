# Registry Refactoring: Separation of Concerns

## Context
Currently, `Aspire-Full.Api` references `Aspire-Full.DockerRegistry` and calls `AddDockerRegistryClient`.
This extension method registers not only the HTTP Client but also the `GarbageCollectorService` (Hosted Service) and other server-side components.
This causes the API service to run registry maintenance tasks, which is incorrect.

## Goals
1.  **Separate Client and Server Logic**: Split the configuration extension into `AddDockerRegistryClient` (pure client) and `AddDockerRegistryServer` (maintenance, GC).
2.  **Fix API Responsibility**: Ensure `Aspire-Full.Api` only acts as a client.
3.  **Enable Registry Service**: Ensure `Aspire-Full.DockerRegistry` service runs the maintenance tasks.

## Changes
- **Aspire-Full.DockerRegistry**:
    - Refactor `ServiceCollectionExtensions.cs`.
    - Update `Program.cs` to call `AddDockerRegistryServer`.
- **Aspire-Full.Api**:
    - Verify it uses `AddDockerRegistryClient` (which will now be client-only).
