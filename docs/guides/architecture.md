# Architecture Overview

## System Design

Aspire-Full is a distributed application orchestrator built on .NET Aspire, designed for cloud-native development with AI-assisted tooling.

```
┌─────────────────────────────────────────────────────────────┐
│                     Host Machine                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Docker Environment                       │   │
│  │  ┌───────────────────┐  ┌───────────────────────┐   │   │
│  │  │   DevContainer    │  │   Aspire Dashboard    │   │   │
│  │  │  ┌─────────────┐  │  │  ┌─────────────────┐  │   │   │
│  │  │  │ .NET 10 SDK │  │  │  │  Telemetry UI   │  │   │   │
│  │  │  │ Aspire CLI  │  │  │  │  :18888         │  │   │   │
│  │  │  │ Docker CLI  │  │  │  └─────────────────┘  │   │   │
│  │  │  │ GitHub CLI  │  │  │  ┌─────────────────┐  │   │   │
│  │  │  └─────────────┘  │  │  │  OTLP Endpoint  │  │   │   │
│  │  └───────────────────┘  │  │  :18889         │  │   │   │
│  │           │              │  └─────────────────┘  │   │   │
│  │           │              └───────────────────────┘   │   │
│  │           │                         ▲               │   │
│  │           └─────────────────────────┘               │   │
│  │              OpenTelemetry                           │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  Named Volumes (NVMe):                                     │
│  ├── aspire-nuget-cache                                    │
│  ├── aspire-dotnet-tools                                   │
│  ├── aspire-aspire-cli                                     │
│  ├── aspire-vscode-extensions                              │
│  ├── aspire-workspace                                      │
│  └── aspire-dashboard-data                                 │
└─────────────────────────────────────────────────────────────┘
```

## Components

### AppHost

The AppHost is the orchestrator for the distributed application:

- **Entry Point**: `Aspire-Full/AppHost.cs`
- **SDK**: `Aspire.AppHost.Sdk` v13.0.1
- **Framework**: .NET 10.0

### API Service

RESTful API with Entity Framework Core:

- **Entry Point**: `Aspire-Full.Api/Program.cs`
- **Database**: PostgreSQL via `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`
- **ORM**: Entity Framework Core with soft-delete pattern

### Web Frontend

React SPA with Vite:

- **Entry Point**: `Aspire-Full.Web/src/main.tsx`
- **Framework**: React 19 + TypeScript
- **UI**: Semantic UI React
- **Build**: Vite 6

### Aspire Dashboard

Standalone container providing:
- Real-time telemetry visualization
- Distributed tracing
- Metrics collection
- Log aggregation

### DevContainer

Isolated development environment with:
- Full .NET toolchain
- Docker-in-Docker capability
- GitHub CLI with AI extensions
- Pre-warmed NuGet cache

## Data Flow

```
┌──────────────┐     OTLP      ┌────────────────────┐
│  Application │ ────────────► │  Aspire Dashboard  │
│   Services   │   :18889      │    Collector       │
└──────────────┘               └────────────────────┘
                                         │
                                         ▼
                               ┌────────────────────┐
                               │   Dashboard UI     │
                               │      :18888        │
                               └────────────────────┘
```

## Volume Strategy

All persistent data is stored in named Docker volumes, not host mounts:

| Volume | Purpose | Contents |
|--------|---------|----------|
| `aspire-nuget-cache` | Package cache | NuGet packages |
| `aspire-dotnet-tools` | Global tools | dotnet-ef, etc. |
| `aspire-aspire-cli` | Aspire CLI | CLI binaries |
| `aspire-vscode-extensions` | VS Code | Extensions |
| `aspire-workspace` | Source code | Cloned repo |
| `aspire-dashboard-data` | Dashboard | Telemetry data |

## Network Architecture

```
┌─────────────────────────────────────────┐
│           aspire-network                 │
│                                          │
│  ┌──────────────┐  ┌──────────────────┐ │
│  │ devcontainer │  │ aspire-dashboard │ │
│  │              │◄─┤                  │ │
│  │              │  │  :18888 (UI)     │ │
│  │              │  │  :18889 (OTLP)   │ │
│  └──────────────┘  └──────────────────┘ │
└─────────────────────────────────────────┘
```

## Security Considerations

### Development Mode
- Dashboard runs unsecured for local development
- OTLP endpoint accepts anonymous connections
- HTTP transport allowed

### Production Recommendations
- Enable authentication on dashboard
- Use HTTPS for all endpoints
- Implement proper secrets management
- Use Azure Key Vault or similar

## Database Layer

### Entity Framework Core

```
┌─────────────────────────────────────────────────────────────┐
│                    AppDbContext                              │
├─────────────────────────────────────────────────────────────┤
│  DbSet<Item> Items                                          │
│  DbSet<User> Users                                          │
├─────────────────────────────────────────────────────────────┤
│  Global Query Filters:                                       │
│  - Users: IsActive == true (soft-delete)                    │
├─────────────────────────────────────────────────────────────┤
│  Relationships:                                              │
│  - User 1:N Items (SetNull on delete)                       │
└─────────────────────────────────────────────────────────────┘
```

### Soft-Delete Pattern

Users are never physically deleted by default:

```
[Active User] ─── DELETE ───► [Inactive User] ─── Reactivate ───► [Active User]
      │                              │
      │                              │
      └── Admin Permanent Delete ────┘──► [Removed from DB]
```

### Upsert/Downsert Operations

| Operation | Description | Effect |
|-----------|-------------|--------|
| **Upsert** | Create or reactivate | New user or restore soft-deleted |
| **Downsert** | Soft delete | Sets `IsActive=false`, `DeletedAt` |
