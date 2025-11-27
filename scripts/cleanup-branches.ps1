# Clean up local Git branches
# Uses gh-poi to safely remove merged/stale branches

param(
    [switch]$DryRun,
    [switch]$Force
)

Write-Host "🧹 Cleaning up local branches..." -ForegroundColor Cyan

# Check if gh-poi is installed
$poiInstalled = gh extension list | Select-String "gh-poi"
if (-not $poiInstalled) {
    Write-Host "📦 Installing gh-poi..." -ForegroundColor Yellow
    gh extension install seachicken/gh-poi
}

Push-Location $PSScriptRoot\..

try {
    if ($DryRun) {
        Write-Host "🔍 Dry run - showing branches that would be deleted:" -ForegroundColor Yellow
        gh poi --dry-run
    } elseif ($Force) {
        gh poi --force
    } else {
        gh poi
    }
} finally {
    Pop-Location
}
