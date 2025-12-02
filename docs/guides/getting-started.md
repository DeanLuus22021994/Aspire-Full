# Getting Started with Aspire-Full

## Prerequisites

- .NET 10 SDK (10.0.100 or later)
- Docker Desktop
- Visual Studio Code with Dev Containers extension
- GitHub CLI (optional but recommended)

## Installation

### Option 1: DevContainer (Recommended)

1. Clone the repository:
```bash
git clone https://github.com/DeanLuus22021994/Aspire-Full.git
cd Aspire-Full
```

2. Open in VS Code and reopen in container when prompted

3. The devcontainer includes:
   - .NET 10 SDK
   - Aspire CLI
   - Docker CLI
   - GitHub CLI with extensions
   - All required tools pre-configured

### Option 2: Local Development

1. Install .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0

2. Install Aspire CLI:
```powershell
Invoke-RestMethod -Uri "https://aspire.dev/install.ps1" | Invoke-Expression
```

3. Clone and restore:
```bash
git clone https://github.com/DeanLuus22021994/Aspire-Full.git
cd Aspire-Full
dotnet restore
```

## Running the Application

### Using Aspire CLI
```bash
aspire run
```

### Using dotnet CLI
```bash
dotnet run --project Aspire-Full
```

### Using Docker Compose
```bash
cd .devcontainer
docker compose up -d
```

## Accessing Services

| Service | URL | Description |
|---------|-----|-------------|
| Aspire Dashboard | http://localhost:18888 | Telemetry and monitoring |
| OTLP Endpoint | http://localhost:18889 | OpenTelemetry ingestion |

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL` | `http://aspire-dashboard:18889` | OTLP endpoint |
| `ASPIRE_ALLOW_UNSECURED_TRANSPORT` | `true` | Allow HTTP in development |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | `1` | Disable telemetry |

## Next Steps

- [Architecture Overview](architecture.md) - Understand the system design
- [DevContainer Setup](devcontainer.md) - Configure your development environment
- [GitHub Tooling](github-tooling.md) - Leverage CLI extensions
