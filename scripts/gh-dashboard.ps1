# GitHub Dashboard Launcher
# Opens the gh-dash terminal UI for GitHub

Write-Host "🚀 Launching GitHub Dashboard..." -ForegroundColor Cyan

# Check if gh-dash is installed
$dashInstalled = gh extension list | Select-String "gh-dash"
if (-not $dashInstalled) {
    Write-Host "📦 Installing gh-dash..." -ForegroundColor Yellow
    gh extension install dlvhdr/gh-dash
}

gh dash
