#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Initialize all required Docker volumes and network for the Aspire-Full stack.

.DESCRIPTION
    Creates all named Docker volumes and the network required by the development
    environment, GitHub Actions runner, and supporting services.

.EXAMPLE
    ./init-docker-infra.ps1
#>

$ErrorActionPreference = "Stop"

# All required named volumes
$RequiredVolumes = @(
    # Development container volumes
    @{ Name = "aspire-nuget-cache"; Description = "NuGet package cache" },
    @{ Name = "aspire-dotnet-tools"; Description = ".NET global tools" },
    @{ Name = "aspire-aspire-cli"; Description = "Aspire CLI data" },
    @{ Name = "aspire-vscode-extensions"; Description = "VS Code server extensions" },
    @{ Name = "aspire-workspace"; Description = "Workspace source code" },
    @{ Name = "aspire-dashboard-data"; Description = "Aspire Dashboard data" },
    @{ Name = "aspire-docker-data"; Description = "Docker-in-Docker daemon data" },
    @{ Name = "aspire-docker-certs"; Description = "Docker TLS certificates" },
    # GitHub Actions Runner volumes
    @{ Name = "aspire-runner-data"; Description = "Runner configuration and state" },
    @{ Name = "aspire-runner-work"; Description = "Runner work directory" },
    @{ Name = "aspire-runner-nuget"; Description = "Runner NuGet cache" },
    @{ Name = "aspire-runner-npm"; Description = "Runner npm cache" },
    @{ Name = "aspire-runner-toolcache"; Description = "GitHub Actions tool cache" },
    # GitHub MCP service volumes
    @{ Name = "aspire-github-mcp-data"; Description = "GitHub MCP data" },
    @{ Name = "aspire-github-mcp-logs"; Description = "GitHub MCP logs" },
    @{ Name = "aspire-github-mcp-cache"; Description = "GitHub MCP cache" },
    # Database volumes
    @{ Name = "aspire-postgres-data"; Description = "PostgreSQL database" },
    @{ Name = "aspire-redis-data"; Description = "Redis cache" }
)

$RequiredNetwork = "aspire-network"

function Write-Info { param([string]$Message) Write-Host "[INFO] " -NoNewline -ForegroundColor Green; Write-Host $Message }
function Write-Warn { param([string]$Message) Write-Host "[WARN] " -NoNewline -ForegroundColor Yellow; Write-Host $Message }
function Write-Err { param([string]$Message) Write-Host "[ERROR] " -NoNewline -ForegroundColor Red; Write-Host $Message }

function Test-DockerRunning {
    try {
        docker info 2>&1 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

# Check Docker is running
if (-not (Test-DockerRunning)) {
    Write-Err "Docker is not running. Please start Docker Desktop first."
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Aspire-Full Docker Infrastructure" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create network
Write-Info "Creating Docker network..."
$networkExists = docker network ls -q --filter "name=^${RequiredNetwork}$" 2>$null
if (-not $networkExists) {
    docker network create $RequiredNetwork | Out-Null
    Write-Host "  Created: $RequiredNetwork" -ForegroundColor Green
}
else {
    Write-Host "  Exists: $RequiredNetwork" -ForegroundColor DarkGray
}

Write-Host ""
Write-Info "Creating Docker volumes..."

$created = 0
$existing = 0

foreach ($vol in $RequiredVolumes) {
    $exists = docker volume ls -q --filter "name=^$($vol.Name)$" 2>$null
    if (-not $exists) {
        docker volume create $vol.Name | Out-Null
        Write-Host "  Created: $($vol.Name)" -ForegroundColor Green
        Write-Host "           $($vol.Description)" -ForegroundColor DarkGray
        $created++
    }
    else {
        Write-Host "  Exists: $($vol.Name)" -ForegroundColor DarkGray
        $existing++
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Volumes created: $created" -ForegroundColor Green
Write-Host "  Volumes existing: $existing" -ForegroundColor DarkGray
Write-Host "  Total: $($RequiredVolumes.Count)" -ForegroundColor Cyan
Write-Host ""
Write-Info "Infrastructure ready!"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Open in VS Code Dev Container:" -ForegroundColor White
Write-Host "     code --folder-uri vscode-remote://dev-container+$(([System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((Get-Location).Path))).TrimEnd('='))/workspace" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  2. Or start services manually:" -ForegroundColor White
Write-Host "     docker compose -f .devcontainer/docker-compose.yml up -d" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  3. Configure GitHub Actions runner:" -ForegroundColor White
Write-Host "     ./scripts/manage-runner.ps1 -Action setup -Token <github-pat>" -ForegroundColor DarkGray
Write-Host ""
