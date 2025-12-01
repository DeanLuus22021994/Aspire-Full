# Plan: Strict Codebase Alignment for AI

## Steps

1.  **Enforce AI Context Rules**
    - Create `.github/copilot-instructions.md`.
    - Define rules: `Aspire-Full.Shared` for DTOs, `Result<T>` usage, No raw SQL, etc.

2.  **Complete Knowledge Graph**
    - Rewrite `llms.txt`.
    - Index all projects: AI (Agents, Tensor), Core (Api, Gateway), Infra (Docker, DevContainer).

3.  **Enhance Code Intelligence**
    - Update `Directory.Build.props`.
    - Enable `GenerateDocumentationFile`.
    - Enforce `TreatWarningsAsErrors` for XML doc warnings (CS1570, CS1572).

4.  **Synchronize Solution**
    - Scan directory for `.csproj` files.
    - Update `Aspire-Full.slnx` to include missing projects (e.g., `Aspire-Full.Pipeline`).

5.  **Standardize Syntax**
    - Update `.editorconfig`.
    - Enforce file-scoped namespaces, global usings, and naming conventions.
