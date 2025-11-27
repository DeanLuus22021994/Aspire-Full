# GitHub PAT Runner Token Script
# Retrieves self-hosted runner registration token using a GitHub PAT
# Requires: repo scope for repo-level runners, admin:org for org-level runners

param(
    [Parameter(Mandatory=$true)]
    [string]$PAT,

    [Parameter(Mandatory=$true)]
    [string]$Owner,

    [Parameter(Mandatory=$true)]
    [string]$Repo,

    [ValidateSet("repo", "org")]
    [string]$Level = "repo"
)

$ErrorActionPreference = "Stop"

Write-Host "🔑 GitHub Runner Token Retrieval" -ForegroundColor Cyan
Write-Host ""

# Set up headers with PAT
$headers = @{
    "Accept" = "application/vnd.github+json"
    "Authorization" = "Bearer $PAT"
    "X-GitHub-Api-Version" = "2022-11-28"
}

try {
    if ($Level -eq "repo") {
        # Repository-level runner token
        $url = "https://api.github.com/repos/$Owner/$Repo/actions/runners/registration-token"
        Write-Host "📍 Getting repository runner token for $Owner/$Repo" -ForegroundColor Yellow
    }
    else {
        # Organization-level runner token
        $url = "https://api.github.com/orgs/$Owner/actions/runners/registration-token"
        Write-Host "📍 Getting organization runner token for $Owner" -ForegroundColor Yellow
    }

    $response = Invoke-RestMethod -Uri $url -Method Post -Headers $headers

    Write-Host ""
    Write-Host "✅ Runner registration token retrieved successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Token: $($response.token)" -ForegroundColor White
    Write-Host "Expires: $($response.expires_at)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "📋 Use this token with ./config.sh --token <TOKEN>" -ForegroundColor Cyan

    # Return the token for programmatic use
    return $response
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__

    Write-Host "❌ Failed to retrieve runner token" -ForegroundColor Red
    Write-Host ""

    switch ($statusCode) {
        401 { Write-Host "Authentication failed. Check your PAT has correct permissions." -ForegroundColor Red }
        403 { Write-Host "Forbidden. PAT needs 'repo' scope for repo runners or 'admin:org' for org runners." -ForegroundColor Red }
        404 { Write-Host "Repository/Organization not found or PAT lacks access." -ForegroundColor Red }
        default { Write-Host "Error: $_" -ForegroundColor Red }
    }

    Write-Host ""
    Write-Host "Required PAT scopes:" -ForegroundColor Yellow
    Write-Host "  - Repository runners: 'repo' scope" -ForegroundColor Gray
    Write-Host "  - Organization runners: 'admin:org' scope" -ForegroundColor Gray

    exit 1
}
