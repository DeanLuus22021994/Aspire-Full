# GitHub Copilot CLI Helper
# Shortcuts for common Copilot CLI commands

param(
    [Parameter(Position = 0)]
    [ValidateSet("suggest", "explain", "config", "alias")]
    [string]$Command = "suggest",

    [Parameter(Position = 1, ValueFromRemainingArguments)]
    [string[]]$Query
)

$ErrorActionPreference = "Stop"

# Check if gh-copilot is installed
$copilotInstalled = gh extension list | Select-String "gh-copilot"
if (-not $copilotInstalled) {
    Write-Host "📦 Installing gh-copilot..." -ForegroundColor Yellow
    gh extension install github/gh-copilot
}

switch ($Command) {
    "suggest" {
        if ($Query) {
            gh copilot suggest ($Query -join " ")
        } else {
            Write-Host "Usage: .\copilot-helper.ps1 suggest 'your query here'" -ForegroundColor Yellow
            Write-Host "Example: .\copilot-helper.ps1 suggest 'list all docker containers'" -ForegroundColor Gray
        }
    }
    "explain" {
        if ($Query) {
            gh copilot explain ($Query -join " ")
        } else {
            Write-Host "Usage: .\copilot-helper.ps1 explain 'command to explain'" -ForegroundColor Yellow
            Write-Host "Example: .\copilot-helper.ps1 explain 'docker ps -a'" -ForegroundColor Gray
        }
    }
    "config" {
        gh copilot config
    }
    "alias" {
        Write-Host "🔧 Setting up Copilot aliases..." -ForegroundColor Cyan
        gh copilot alias -- pwsh | Out-File -FilePath $PROFILE -Append -Encoding utf8
        Write-Host "✅ Aliases added to profile. Restart PowerShell to use." -ForegroundColor Green
        Write-Host "   You can now use: ghcs (suggest), ghce (explain)" -ForegroundColor Gray
    }
}
