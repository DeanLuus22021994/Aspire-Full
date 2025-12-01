# Spec: Migrate Scripts to C# Pipeline CLI

## Goal
Consolidate all automation scripts currently in the `scripts/` directory into the `Aspire-Full.Pipeline` C# CLI tool. This will provide a single, cross-platform, type-safe entry point for all development, CI/CD, and infrastructure tasks.

## Scope
- **Source**: All PowerShell (`.ps1`) and Shell (`.sh`, `.cmd`) scripts in `scripts/`.
- **Destination**: `Aspire-Full.Pipeline` project.
- **Technology**: .NET 9, System.CommandLine, Spectre.Console.

## Functional Requirements
1.  **Infrastructure Management**: Init Docker infra, setup volumes, check networks.
2.  **CI/CD Operations**: Manage GitHub runners, tokens, caching, SBOM generation.
3.  **Developer Workflows**: Branch cleanup, GitHub extension setup, dashboard access.
4.  **AI Operations**: Agent workflows, Copilot helpers, model management.
5.  **Documentation**: Generate docs and changelogs.
6.  **Host Management**: Enhanced `host` command to replace `Start-Aspire.ps1`.

## Non-Functional Requirements
- **Cross-Platform**: Must work on Windows, Linux, and macOS.
- **Type Safety**: Use strong typing for arguments and options.
- **User Experience**: Use `Spectre.Console` for rich output (spinners, tables, colors).
- **Maintainability**: Group commands into logical modules/classes.

## Architecture
The `Aspire-Full.Pipeline` project will be organized into modules:
- `InfraModule`: Docker, Volumes, Network.
- `CiModule`: GitHub Actions, Runners.
- `DevModule`: Git, Extensions, Dashboard.
- `AiModule`: Agents, Models.
- `DocsModule`: Documentation.

Each module will register its commands with the root command in `Program.cs`.
