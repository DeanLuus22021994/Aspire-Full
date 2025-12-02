#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build and run GPU workers with privileged access for zero-latency compute.

.DESCRIPTION
    This script:
    1. Creates all required Docker volumes
    2. Builds the devcontainer with Python 3.15t + CuPy
    3. Starts 2 GPU workers with 1GB VRAM each
    4. Ensures privileged access for direct GPU compute

.PARAMETER Build
    Force rebuild of all images

.PARAMETER WorkersOnly
    Only start GPU workers, skip other services
#>

param(
    [switch]$Build,
    [switch]$WorkersOnly
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

Write-Host "🚀 GPU Workers Setup - Python 3.15t Free-Threaded + CuPy" -ForegroundColor Cyan
Write-Host "   2 x 1GB VRAM workers + 1GB Qdrant = 3GB total" -ForegroundColor Gray
Write-Host ""

# =============================================================================
# Step 1: Create all required Docker volumes
# =============================================================================
Write-Host "📦 Creating Docker volumes..." -ForegroundColor Yellow

$volumes = @(
    "aspire-nuget-cache",
    "aspire-dotnet-tools",
    "aspire-aspire-cli",
    "aspire-vscode-extensions",
    "aspire-workspace",
    "aspire-dashboard-data",
    "aspire-docker-data",
    "aspire-docker-certs",
    "aspire-buildkit-cache",
    "aspire-ccache",
    "aspire-cuda-cache",
    "aspire-runner-data",
    "aspire-runner-work",
    "aspire-runner-nuget",
    "aspire-runner-npm",
    "aspire-runner-toolcache",
    "aspire-github-mcp-data",
    "aspire-github-mcp-logs",
    "aspire-github-mcp-cache",
    "aspire-uv-cache",
    "aspire-models",
    "aspire-logs"
)

foreach ($vol in $volumes) {
    $exists = docker volume ls -q --filter "name=$vol" 2>$null
    if (-not $exists) {
        docker volume create $vol | Out-Null
        Write-Host "  ✅ Created volume: $vol" -ForegroundColor Green
    } else {
        Write-Host "  ⏭️  Volume exists: $vol" -ForegroundColor DarkGray
    }
}

# Create network if not exists
$networkExists = docker network ls -q --filter "name=aspire-network" 2>$null
if (-not $networkExists) {
    docker network create aspire-network | Out-Null
    Write-Host "  ✅ Created network: aspire-network" -ForegroundColor Green
}

Write-Host ""

# =============================================================================
# Step 2: Build images
# =============================================================================
if ($Build) {
    Write-Host "🔨 Building Docker images..." -ForegroundColor Yellow

    $buildArgs = @(
        "compose",
        "-f", ".devcontainer/docker-compose.yml",
        "build",
        "--parallel"
    )

    if ($WorkersOnly) {
        $buildArgs += "gpu-worker-1", "gpu-worker-2"
    }

    & docker @buildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✅ Build complete" -ForegroundColor Green
    Write-Host ""
}

# =============================================================================
# Step 3: Verify GPU access
# =============================================================================
Write-Host "🎮 Verifying GPU access..." -ForegroundColor Yellow

try {
    $gpuInfo = nvidia-smi --query-gpu=name,memory.total,driver_version --format=csv,noheader 2>$null
    if ($gpuInfo) {
        Write-Host "  ✅ GPU detected: $gpuInfo" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  nvidia-smi not available, GPU may not be accessible" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ⚠️  Could not query GPU info" -ForegroundColor Yellow
}
Write-Host ""

# =============================================================================
# Step 4: Start services
# =============================================================================
Write-Host "🚀 Starting GPU workers..." -ForegroundColor Yellow

$upArgs = @(
    "compose",
    "-f", ".devcontainer/docker-compose.yml",
    "up", "-d"
)

if ($WorkersOnly) {
    $upArgs += "gpu-worker-1", "gpu-worker-2"
} else {
    # Start dashboard first, then workers
    & docker compose -f .devcontainer/docker-compose.yml up -d aspire-dashboard
    Start-Sleep -Seconds 3
    $upArgs += "gpu-worker-1", "gpu-worker-2"
}

& docker @upArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to start services!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ GPU Workers started!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Worker Status:" -ForegroundColor Cyan
docker ps --filter "name=aspire-gpu-worker" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

Write-Host ""
Write-Host "🔍 Check worker logs:" -ForegroundColor Gray
Write-Host "   docker logs -f aspire-gpu-worker-1" -ForegroundColor DarkGray
Write-Host "   docker logs -f aspire-gpu-worker-2" -ForegroundColor DarkGray

Write-Host ""
Write-Host "🎯 GPU Memory Allocation:" -ForegroundColor Cyan
Write-Host "   Worker 1: 1GB VRAM (hot standby)" -ForegroundColor Gray
Write-Host "   Worker 2: 1GB VRAM (hot standby)" -ForegroundColor Gray
Write-Host "   Qdrant:   1GB VRAM (same subnet)" -ForegroundColor Gray
Write-Host "   Total:    3GB VRAM" -ForegroundColor Gray
