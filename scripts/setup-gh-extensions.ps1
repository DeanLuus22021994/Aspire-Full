# GitHub CLI Extensions Setup Script
# Run this to install all recommended gh extensions

$extensions = @(
    "github/gh-copilot",           # AI assistant in terminal
    "github/gh-models",            # GitHub Models API access
    "github/gh-actions-importer",  # Migrate CI/CD to GitHub Actions
    "actions/gh-actions-cache",    # Manage Actions cache
    "nektos/gh-act",               # Run GitHub Actions locally
    "dlvhdr/gh-dash",              # Rich terminal dashboard
    "advanced-security/gh-sbom",   # Generate SBOMs
    "github/gh-projects",          # Manage GitHub Projects
    "seachicken/gh-poi",           # Clean up local branches
    "gennaro-tedesco/gh-s",        # Interactive repo search
    "mislav/gh-branch",            # Fuzzy branch finder
    "chelnak/gh-changelog",        # Generate changelogs
    "meiji163/gh-notify",          # GitHub notifications
    "githubnext/gh-aw"             # Agentic Workflows
)

Write-Host "🚀 Installing GitHub CLI Extensions..." -ForegroundColor Cyan
Write-Host ""

foreach ($ext in $extensions) {
    Write-Host "📦 Installing $ext..." -ForegroundColor Yellow
    gh extension install $ext 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Installed" -ForegroundColor Green
    } else {
        Write-Host "   ⏭️ Already installed or skipped" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "✅ All extensions installed!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Installed extensions:" -ForegroundColor Cyan
gh extension list
