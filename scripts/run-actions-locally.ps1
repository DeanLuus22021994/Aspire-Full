# Run GitHub Actions Locally
# Uses gh-act extension to run workflows locally with Docker

param(
    [string]$Workflow = "",
    [string]$Job = "",
    [string]$EventName = "push",
    [switch]$List,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "🎬 GitHub Actions Local Runner" -ForegroundColor Cyan
Write-Host ""

# Check if gh-act is installed
$actInstalled = gh extension list | Select-String "gh-act"
if (-not $actInstalled) {
    Write-Host "❌ gh-act not installed. Installing..." -ForegroundColor Red
    gh extension install nektos/gh-act
}

# Check if Docker is running
docker info 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Docker is not running. Please start Docker Desktop." -ForegroundColor Red
    exit 1
}

Push-Location $PSScriptRoot\..

try {
    if ($List) {
        Write-Host "📋 Available workflows:" -ForegroundColor Yellow
        gh act -l
        exit 0
    }

    $actArgs = @()

    if ($EventName) {
        $actArgs += $EventName
    }

    if ($Workflow) {
        $actArgs += "-W"
        $actArgs += ".github/workflows/$Workflow"
    }

    if ($Job) {
        $actArgs += "-j"
        $actArgs += $Job
    }

    if ($DryRun) {
        $actArgs += "-n"
        Write-Host "🔍 Dry run mode enabled" -ForegroundColor Yellow
    }

    Write-Host "🚀 Running: gh act $($actArgs -join ' ')" -ForegroundColor Green
    Write-Host ""

    gh act @actArgs
