# Aspire-Full

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Aspire](https://img.shields.io/badge/Aspire-13.0.1-512BD4)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![React](https://img.shields.io/badge/React-19-61DAFB?logo=react)](https://react.dev/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A full-stack .NET Aspire distributed application demonstrating modern cloud-native development patterns with Entity Framework Core, PostgreSQL, and React.

## Features

- **Distributed Application** - .NET Aspire orchestration with service defaults
- **RESTful API** - ASP.NET Core Minimal APIs with OpenAPI
- **Entity Framework Core** - PostgreSQL with soft-delete pattern
- **User Management** - Upsert/downsert operations, role-based access
- **React Frontend** - Vite + TypeScript + Semantic UI
- **Observability** - OpenTelemetry with Aspire Dashboard
- **Testing** - Unit tests (xUnit) and E2E tests (NUnit)
- **DevContainer** - Docker-based development environment

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Node.js 22+](https://nodejs.org/) (for frontend)

### Run the Application

```bash
# Clone the repository
git clone https://github.com/DeanLuus22021994/Aspire-Full.git
cd Aspire-Full

# Run with Aspire AppHost
dotnet run --project Aspire-Full
```

The Aspire Dashboard will be available at `http://localhost:18888`

### Run Tests

```bash
# Unit tests
dotnet test Aspire-Full.Tests.Unit

# E2E tests (requires running application)
dotnet test Aspire-Full.Tests.E2E

# All tests with PowerShell script
./scripts/run-tests.ps1
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Aspire AppHost                           │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │
│  │   API       │  │   Web       │  │   PostgreSQL        │ │
│  │ (ASP.NET)   │  │ (React)     │  │   (Aspire.Npgsql)   │ │
│  └─────────────┘  └─────────────┘  └─────────────────────┘ │
│         │                │                    │             │
│         └────────────────┼────────────────────┘             │
│                          │                                   │
│                  ┌───────▼───────┐                          │
│                  │   Dashboard   │                          │
│                  │  (Telemetry)  │                          │
│                  └───────────────┘                          │
└─────────────────────────────────────────────────────────────┘
```

## API Endpoints

### Items API (`/api/items`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/items` | List all items |
| GET | `/api/items/{id}` | Get item by ID |
| POST | `/api/items` | Create item |
| PUT | `/api/items/{id}` | Update item |
| DELETE | `/api/items/{id}` | Delete item |

### Users API (`/api/users`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users` | List active users |
| POST | `/api/users` | **Upsert**: Create or reactivate |
| DELETE | `/api/users/{id}` | **Downsert**: Soft delete |
| POST | `/api/users/{id}/login` | Record login |

### Admin API (`/api/admin`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/users` | All users (including deleted) |
| POST | `/api/admin/users/{id}/promote` | Promote to Admin |
| POST | `/api/admin/users/{id}/reactivate` | Reactivate user |
| DELETE | `/api/admin/users/{id}/permanent` | Permanent delete |
| GET | `/api/admin/stats` | Admin statistics |

## Database Schema

### Users Table
- Soft-delete enabled via `IsActive` flag
- Global query filter excludes deleted users
- Roles: `User` (0), `Admin` (1)

### Items Table
- Foreign key to Users (`CreatedByUserId`)
- SetNull on user delete

## Project Structure

```
Aspire-Full/
├── Aspire-Full/              # AppHost orchestrator
├── Aspire-Full.Api/          # REST API
│   ├── Controllers/          # API endpoints
│   ├── Data/                 # EF Core context
│   └── Models/               # Entity models
├── Aspire-Full.Web/          # React frontend
│   └── src/
│       ├── components/       # UI components
│       └── services/         # API client
├── Aspire-Full.ServiceDefaults/ # Shared config
├── Aspire-Full.Tests.Unit/   # Unit tests
├── Aspire-Full.Tests.E2E/    # E2E tests
├── docs/                     # Documentation
└── scripts/                  # Automation
```

## Documentation

- [Getting Started](docs/guides/getting-started.md)
- [Architecture](docs/guides/architecture.md)
- [API Reference](docs/api/index.md)
- [DevContainer](docs/guides/devcontainer.md)

## Technologies

| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | .NET SDK | 10.0.100 |
| Orchestration | Aspire | 13.0.1 |
| Database | PostgreSQL | Latest |
| ORM | EF Core | 10.0.0-preview |
| Frontend | React + Vite | 19 / 6 |
| UI | Semantic UI React | 3.x |
| Unit Tests | xUnit | 2.9.3 |
| E2E Tests | NUnit | 4.3.2 |

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing`)
5. Open a Pull Request

## License

MIT License - see [LICENSE](LICENSE) for details.
