$ErrorActionPreference = "Stop"

$RegistryUrl = "localhost:5000"
$ImageName = "aspire-agents"
$Tag = "latest"
$FullImageName = "$RegistryUrl/$ImageName`:$Tag"

Write-Host "=== Provisioning Aspire Agents ===" -ForegroundColor Cyan

# Check if registry is running
if (!(docker ps --filter "name=registry" --format "{{.Names}}")) {
    Write-Host "Registry container not found. Please start Aspire AppHost first or run 'docker run -d -p 5000:5000 --name registry registry:2'" -ForegroundColor Yellow
    # Optional: Start it temporarily if needed, but better to rely on AppHost
}

Write-Host "Building Agent Image..." -ForegroundColor Green
docker build -t $FullImageName -f Aspire-Full.Python/python-agents/Dockerfile.agent Aspire-Full.Python/python-agents

Write-Host "Pushing to Internal Registry..." -ForegroundColor Green
docker push $FullImageName

Write-Host "=== Provisioning Complete ===" -ForegroundColor Cyan
Write-Host "Image available at: $FullImageName"
