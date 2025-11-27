# Generate Changelog
# Uses gh-changelog to generate release notes

param(
    [string]$Version = "",
    [string]$Output = "CHANGELOG.md"
)

Write-Host "📝 Generating Changelog..." -ForegroundColor Cyan

# Check if gh-changelog is installed
$changelogInstalled = gh extension list | Select-String "gh-changelog"
if (-not $changelogInstalled) {
    Write-Host "📦 Installing gh-changelog..." -ForegroundColor Yellow
    gh extension install chelnak/gh-changelog
}

Push-Location $PSScriptRoot\..

try {
    if ($Version) {
        gh changelog new --next-version $Version
    } else {
        gh changelog new
    }

    Write-Host "✅ Changelog updated: $Output" -ForegroundColor Green
} finally {
    Pop-Location
}
