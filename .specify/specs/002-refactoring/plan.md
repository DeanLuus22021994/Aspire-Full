# Refactoring Plan

## Phase 1: Embeddings & Vector Store
- [x] Create `Aspire-Full.Embeddings/Extensions/ServiceCollectionExtensions.cs`.
- [x] Refactor `Aspire-Full.Gateway/Program.cs` to use the new extension.
- [x] Analyze `Aspire-Full.Gateway/Services/VectorStoreService.cs` vs `Aspire-Full.VectorStore/VectorStoreService.cs`. (Gateway uses UserVectorService which consumes the shared library)
- [x] Refactor Gateway service to consume the shared library.

## Phase 2: WebAssembly Standardization
- [ ] Add `Aspire-Full.ServiceDefaults` project reference to `Aspire-Full.WebAssembly`.
- [ ] Update `Aspire-Full.WebAssembly/Program.cs` to call `AddServiceDefaults()`.

## Phase 3: AppHost Cleanup
- [ ] Create `Aspire-Full/Configuration/AppHostConstants.cs`.
- [ ] Replace hardcoded strings in `Aspire-Full/AppHost.cs`.

## Phase 4: API Cleanup
- [x] Create `Aspire-Full.Api/Extensions/TensorServiceCollectionExtensions.cs`.
- [x] Refactor `Aspire-Full.Api/Program.cs` to use `AddTensorOrchestration`.
- [x] Refactor `Aspire-Full.Api/Program.cs` to use `AddDockerRegistryClient`.
