# Registry Refactoring Plan

- [ ] Refactor `Aspire-Full.DockerRegistry/Configuration/ServiceCollectionExtensions.cs`:
    - Keep `AddDockerRegistryClient` for `IDockerRegistryClient` registration.
    - Create `AddDockerRegistryServer` for `IGarbageCollector`, `GarbageCollectorService`, `IBuildxWorkerFactory`.
- [ ] Update `Aspire-Full.DockerRegistry/Program.cs`:
    - Call `builder.Services.AddDockerRegistryServer(builder.Configuration)`.
- [ ] Verify `Aspire-Full.Api/Program.cs`:
    - Ensure it calls `AddDockerRegistryClient`.
- [ ] Build and Verify.
