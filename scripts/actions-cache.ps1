# Manage GitHub Actions Cache
# View and clear Actions cache for the repository

param(
    [Parameter(Position = 0)]
    [ValidateSet("list", "delete", "clear")]
    [string]$Command = "list",

    [string]$Key = ""
)

$ErrorActionPreference = "Stop"

Write-Host "📦 GitHub Actions Cache Manager" -ForegroundColor Cyan

# Check if gh-actions-cache is installed
$cacheInstalled = gh extension list | Select-String "gh-actions-cache"
if (-not $cacheInstalled) {
    Write-Host "📦 Installing gh-actions-cache..." -ForegroundColor Yellow
    gh extension install actions/gh-actions-cache
}

Push-Location $PSScriptRoot\..

try {
    $repo = gh repo view --json nameWithOwner -q ".nameWithOwner"
    Write-Host "📦 Repository: $repo" -ForegroundColor Yellow

    switch ($Command) {
        "list" {
            Write-Host "📋 Cache entries:" -ForegroundColor Yellow
            gh actions-cache list -R $repo
        }
        "delete" {
            if ($Key) {
                Write-Host "🗑️ Deleting cache: $Key" -ForegroundColor Yellow
                gh actions-cache delete $Key -R $repo --confirm
            } else {
                Write-Host "Usage: .\actions-cache.ps1 delete -Key <cache-key>" -ForegroundColor Yellow
            }
        }
        "clear" {
            Write-Host "🗑️ Clearing all caches..." -ForegroundColor Red
            $caches = gh actions-cache list -R $repo --json key -q ".[].key"
            foreach ($cache in $caches) {
                gh actions-cache delete $cache -R $repo --confirm
            }
            Write-Host "✅ All caches cleared" -ForegroundColor Green
        }
    }
} finally {
    Pop-Location
}
