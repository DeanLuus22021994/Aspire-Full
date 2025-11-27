# Generate SBOM (Software Bill of Materials)
# Uses gh-sbom extension to generate SBOMs

param(
    [string]$Output = "sbom.json",
    [string]$Format = "spdx-json"
)

Write-Host "📋 Generating Software Bill of Materials..." -ForegroundColor Cyan

# Check if gh-sbom is installed
$sbomInstalled = gh extension list | Select-String "gh-sbom"
if (-not $sbomInstalled) {
    Write-Host "📦 Installing gh-sbom..." -ForegroundColor Yellow
    gh extension install advanced-security/gh-sbom
}

Push-Location $PSScriptRoot\..

try {
    $repo = gh repo view --json nameWithOwner -q ".nameWithOwner"
    Write-Host "📦 Repository: $repo" -ForegroundColor Yellow

    gh sbom -r $repo | Out-File -FilePath $Output -Encoding utf8

    Write-Host "✅ SBOM generated: $Output" -ForegroundColor Green
} finally {
    Pop-Location
}
