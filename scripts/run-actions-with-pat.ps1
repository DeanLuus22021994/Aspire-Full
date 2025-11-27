# Run GitHub Workflows with PAT Authentication
# Uses gh-act to run workflows locally with PAT for API access

param(
    [string]$Workflow = "",
    [string]$Job = "",
    [string]$GhEventName = "push",
    [string]$PAT = $env:GITHUB_TOKEN,
    [switch]$List,
    [switch]$DryRun,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "🎬 GitHub Actions Local Runner (PAT Mode)" -ForegroundColor Cyan
Write-Host ""

# Validate PAT
if (-not $PAT -and -not $List) {
    Write-Host "⚠️  No PAT provided. Set GITHUB_TOKEN env var or use -PAT parameter" -ForegroundColor Yellow
    Write-Host "   Some workflows may fail without authentication" -ForegroundColor Gray
    Write-Host ""
}

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

    $ghActParams = @()

    if ($GhEventName) {
        $ghActParams += $GhEventName
    }

    if ($Workflow) {
        $ghActParams += "-W"
        $ghActParams += ".github/workflows/$Workflow"
    }

    if ($Job) {
        $ghActParams += "-j"
        $ghActParams += $Job
    }

    if ($DryRun) {
        $ghActParams += "-n"
        Write-Host "🔍 Dry run mode enabled" -ForegroundColor Yellow
    }

    if ($Verbose) {
        $ghActParams += "-v"
    }

    # Pass secrets to act
    if ($PAT) {
        $ghActParams += "-s"
        $ghActParams += "GITHUB_TOKEN=$PAT"
        Write-Host "🔐 Using provided PAT for authentication" -ForegroundColor Green
    }

    Write-Host "🚀 Running: gh act $($ghActParams -join ' ')" -ForegroundColor Green
    Write-Host ""

    gh act @ghActParams
}
finally {
    Pop-Location
}
