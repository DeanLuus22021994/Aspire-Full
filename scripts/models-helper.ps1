# GitHub Models CLI Helper
# Interact with GitHub Models from the command line

param(
    [Parameter(Position = 0)]
    [ValidateSet("list", "run", "view", "chat")]
    [string]$Command = "list",

    [Parameter(Position = 1)]
    [string]$Model = "",

    [Parameter(Position = 2, ValueFromRemainingArguments)]
    [string[]]$Prompt
)

$ErrorActionPreference = "Stop"

# Check if gh-models is installed
$modelsInstalled = gh extension list | Select-String "gh-models"
if (-not $modelsInstalled) {
    Write-Host "📦 Installing gh-models..." -ForegroundColor Yellow
    gh extension install github/gh-models
}

switch ($Command) {
    "list" {
        Write-Host "📋 Available GitHub Models:" -ForegroundColor Cyan
        gh models list
    }
    "view" {
        if ($Model) {
            gh models view $Model
        } else {
            Write-Host "Usage: .\models-helper.ps1 view <model-name>" -ForegroundColor Yellow
        }
    }
    "run" {
        if ($Model) {
            gh models run $Model
        } else {
            Write-Host "🤖 Starting interactive model selection..." -ForegroundColor Cyan
            gh models run
        }
    }
    "chat" {
        Write-Host "💬 Starting chat mode..." -ForegroundColor Cyan
        if ($Model) {
            gh models run $Model
        } else {
            gh models run
        }
    }
}
