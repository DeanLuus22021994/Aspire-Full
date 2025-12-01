# Plan: Strict Codebase Alignment for AI

## Steps

1.  **Enforce AI Context Rules**
    - Update `.github/copilot-instructions.md` with stricter rules:
        - **Mandatory**: `Directory.Packages.props` for version management (Central Package Management).
        - **Mandatory**: `Result<T>` for all service methods.
        - **Forbidden**: `DateTime.Now` (use `TimeProvider`).
        - **Forbidden**: `Console.WriteLine` (use `ILogger`).

2.  **Centralize Dependency Management**
    - Create `Directory.Packages.props`.
    - Move all package versions from `.csproj` files to `Directory.Packages.props`.
    - Enforce `ManagePackageVersionsCentrally`.

3.  **Enhance Code Intelligence**
    - Update `Directory.Build.props`.
    - Enforce `TreatWarningsAsErrors` for specific high-value warnings (CS8600, CS8602 - Nullability).
    - Enable `EnforceCodeStyleInBuild`.

4.  **Standardize Syntax**
    - Update `.editorconfig`.
    - Enforce `csharp_style_namespace_declarations = file_scoped:error`.
    - Enforce `csharp_style_expression_bodied_methods = true:suggestion`.

5.  **Validation**
    - Run `dotnet build` to verify no regressions.
    - Verify `llms.txt` covers the new structure.
