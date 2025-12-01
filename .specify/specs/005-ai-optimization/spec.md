# AI-Driven Development Optimization Spec

## Goal
Optimize the codebase to maximize GitHub Copilot's effectiveness, enforce strict architectural boundaries, and ensure comprehensive context availability for AI agents.

## Requirements
1.  **Context Rules**: Explicitly define architectural guidelines for Copilot.
2.  **Knowledge Graph**: Index all projects in `llms.txt` for AI discovery.
3.  **Code Intelligence**: Enable XML documentation generation across the solution.
4.  **Solution Sync**: Ensure all projects on disk are included in the solution file.
5.  **Standardization**: Enforce .NET 10 coding conventions via `.editorconfig`.

## Success Criteria
- `.github/copilot-instructions.md` exists and contains project-specific rules.
- `llms.txt` lists all 20+ projects with descriptions.
- `Directory.Build.props` enables `GenerateDocumentationFile`.
- `Aspire-Full.slnx` includes previously orphaned projects.
- `.editorconfig` enforces file-scoped namespaces and other modern conventions.
