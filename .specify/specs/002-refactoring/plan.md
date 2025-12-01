# Refactoring Plan

## Phase 1: Embeddings & Vector Store
- [x] Create `Aspire-Full.Embeddings/Extensions/ServiceCollectionExtensions.cs`.
- [x] Refactor `Aspire-Full.Gateway/Program.cs` to use the new extension.
- [x] Analyze `Aspire-Full.Gateway/Services/VectorStoreService.cs` vs `Aspire-Full.VectorStore/VectorStoreService.cs`. (Gateway uses UserVectorService which consumes the shared library)
- [x] Refactor Gateway service to consume the shared library.

## Phase 2: WebAssembly Standardization
- [x] Add `Aspire-Full.ServiceDefaults` project reference to `Aspire-Full.WebAssembly`. (Attempted, but reverted due to `Microsoft.AspNetCore.App` dependency incompatibility with `browser-wasm`. Kept local extension.)
- [x] Update `Aspire-Full.WebAssembly/Program.cs` to call `AddServiceDefaults()`. (Using local extension for OTLP.)

## Phase 3: AppHost Cleanup
- [x] Create `Aspire-Full/Configuration/AppHostConstants.cs`. (Updated with missing constants)
- [x] Replace hardcoded strings in `Aspire-Full/AppHost.cs`.

## Phase 4: API Cleanup
- [x] Create `Aspire-Full.Api/Extensions/TensorServiceCollectionExtensions.cs`. (Initially created, then moved to `Aspire-Full.Tensor`)
- [x] Refactor `Aspire-Full.Api/Program.cs` to use `AddTensorOrchestration`.
- [x] Refactor `Aspire-Full.Api/Program.cs` to use `AddDockerRegistryClient`.
- [x] Move Tensor Orchestration logic (`TensorJobCoordinator`, `TensorJobStore`, `TensorDtos`) from `Aspire-Full.Api` to `Aspire-Full.Tensor`.
- [x] Move `TensorVectorBridge` to `Aspire-Full.Connectors` to decouple Tensor core from Connectors.
- [x] Update `Aspire-Full.Api` to consume Tensor services from the shared library.

## Phase 5: Gateway & API Dependency Cleanup
- [x] Remove unused `Qdrant.Client.Grpc` using in `UserVectorService.cs`.
- [x] Remove direct reference to `Aspire-Full.Qdrant` from `Aspire-Full.Gateway.csproj` (Enforce abstraction).
- [x] Remove direct reference to `Aspire-Full.Qdrant` from `Aspire-Full.Api.csproj` (Enforce abstraction).
