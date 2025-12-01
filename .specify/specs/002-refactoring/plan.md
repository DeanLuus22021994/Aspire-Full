# Refactoring Plan

## Phase 1: Embeddings & Vector Store
- [ ] Create `Aspire-Full.Embeddings/Extensions/ServiceCollectionExtensions.cs`.
- [ ] Refactor `Aspire-Full.Gateway/Program.cs` to use the new extension.
- [ ] Analyze `Aspire-Full.Gateway/Services/VectorStoreService.cs` vs `Aspire-Full.VectorStore/VectorStoreService.cs`.
- [ ] Refactor Gateway service to consume the shared library.

## Phase 2: WebAssembly Standardization
- [ ] Add `Aspire-Full.ServiceDefaults` project reference to `Aspire-Full.WebAssembly`.
- [ ] Update `Aspire-Full.WebAssembly/Program.cs` to call `AddServiceDefaults()`.

## Phase 3: AppHost Cleanup
- [ ] Create `Aspire-Full/Configuration/AppHostConstants.cs`.
- [ ] Replace hardcoded strings in `Aspire-Full/AppHost.cs`.
