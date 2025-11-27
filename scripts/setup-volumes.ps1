# Setup Named Volumes for Aspire Development
# Creates all required Docker named volumes with proper configuration
# No host mounts - everything persists in Docker managed volumes

$ErrorActionPreference = "Stop"

Write-Host "🐳 Aspire Named Volume Setup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Required volumes
$volumes = @(
    @{ Name = "aspire-nuget-cache"; Description = "NuGet package cache" }
    @{ Name = "aspire-dotnet-tools"; Description = ".NET global tools" }
    @{ Name = "aspire-aspire-cli"; Description = "Aspire CLI data" }
    @{ Name = "aspire-vscode-extensions"; Description = "VS Code extensions" }
    @{ Name = "aspire-workspace"; Description = "Workspace files" }
    @{ Name = "aspire-dashboard-data"; Description = "Aspire Dashboard data" }
    @{ Name = "aspire-docker-data"; Description = "Docker-in-Docker storage" }
    @{ Name = "aspire-docker-certs"; Description = "Docker TLS certificates" }
    @{ Name = "aspire-postgres-data"; Description = "PostgreSQL database" }
    @{ Name = "aspire-redis-data"; Description = "Redis cache data" }
)

# Required network
$network = "aspire-network"

Write-Host "📦 Checking volumes..." -ForegroundColor Yellow
Write-Host ""

$existingVolumes = docker volume ls --format "{{.Name}}"

foreach ($vol in $volumes) {
    if ($existingVolumes -contains $vol.Name) {
        Write-Host "  ✅ $($vol.Name) - exists" -ForegroundColor Green
    }
    else {
        Write-Host "  ⏳ Creating $($vol.Name)..." -ForegroundColor Yellow
        docker volume create $vol.Name | Out-Null
        Write-Host "  ✅ $($vol.Name) - created" -ForegroundColor Green
    }
    Write-Host "     └─ $($vol.Description)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "🌐 Checking network..." -ForegroundColor Yellow

$existingNetworks = docker network ls --format "{{.Name}}"

if ($existingNetworks -contains $network) {
    Write-Host "  ✅ $network - exists" -ForegroundColor Green
}
else {
    Write-Host "  ⏳ Creating $network..." -ForegroundColor Yellow
    docker network create $network | Out-Null
    Write-Host "  ✅ $network - created" -ForegroundColor Green
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "📊 Volume Summary" -ForegroundColor Cyan
Write-Host ""

# Show volume details
docker volume ls --filter "name=aspire-" --format "table {{.Name}}\t{{.Driver}}\t{{.Mountpoint}}"

Write-Host ""
Write-Host "✅ All named volumes are ready!" -ForegroundColor Green
Write-Host ""
Write-Host "🔒 No host mounts configured - all data persists in Docker volumes" -ForegroundColor Cyan
Write-Host ""
