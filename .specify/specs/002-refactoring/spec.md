# Refactoring Specification: Codebase Consolidation and Cleanup

## Context
The codebase has evolved with some redundancy and hardcoded values. Specifically:
1.  **Embedding Registration**: Logic is duplicated or misplaced in the Gateway.
2.  **Vector Store**: The Gateway implements its own Qdrant logic instead of using the shared `Aspire-Full.VectorStore` library.
3.  **WebAssembly**: Lacks standard service defaults (telemetry, health checks).
4.  **AppHost**: Contains hardcoded configuration strings.

## Goals
1.  **Centralize Logic**: Move embedding registration to the Embeddings library.
2.  **Reduce Duplication**: Make Gateway use the shared VectorStore library.
3.  **Standardize Observability**: Ensure WebAssembly uses ServiceDefaults.
4.  **Clean Configuration**: Extract constants in AppHost.

## Architecture Changes
- **Aspire-Full.Embeddings**: New `ServiceCollectionExtensions` for easy registration.
- **Aspire-Full.Gateway**:
    - Remove direct Qdrant client usage in favor of `IVectorStoreService` from the shared library (or wrap it).
    - Rename internal service to `IUserVectorService` if it handles user-specific logic on top of the store.
- **Aspire-Full.WebAssembly**: Add reference to `Aspire-Full.ServiceDefaults`.
- **Aspire-Full**: New `AppHostConstants` class.
