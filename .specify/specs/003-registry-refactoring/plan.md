# Registry Refactoring Plan

- [x] Refactor `Aspire-Full.DockerRegistry/Configuration/ServiceCollectionExtensions.cs`:
    - Keep `AddDockerRegistryClient` for `IDockerRegistryClient` registration.
    - Create `AddDockerRegistryServer` for `IGarbageCollector`, `GarbageCollectorService`, `IBuildxWorkerFactory`.
- [x] Update `Aspire-Full.DockerRegistry/Program.cs`:
    - Call `builder.Services.AddDockerRegistryServer(builder.Configuration)`.
- [x] Verify `Aspire-Full.Api/Program.cs`:
    - Ensure it calls `AddDockerRegistryClient`.
- [x] Build and Verify.
