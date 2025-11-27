# GitHub Agentic Workflows Helper
# Initialize and manage AI-powered workflows

param(
    [Parameter(Position = 0)]
    [ValidateSet("init", "new", "compile", "run", "status", "logs")]
    [string]$Command = "status",

    [Parameter(Position = 1)]
    [string]$WorkflowName = ""
)

$ErrorActionPreference = "Stop"

Write-Host "🤖 GitHub Agentic Workflows" -ForegroundColor Cyan

# Check if gh-aw is installed
$awInstalled = gh extension list | Select-String "gh-aw"
if (-not $awInstalled) {
    Write-Host "📦 Installing gh-aw..." -ForegroundColor Yellow
    gh extension install githubnext/gh-aw
}

Push-Location $PSScriptRoot\..

try {
    switch ($Command) {
        "init" {
            Write-Host "🔧 Initializing repository for agentic workflows..." -ForegroundColor Yellow
            gh aw init
        }
        "new" {
            if ($WorkflowName) {
                Write-Host "📝 Creating new workflow: $WorkflowName" -ForegroundColor Yellow
                gh aw new $WorkflowName
            } else {
                Write-Host "Usage: .\agentic-workflows.ps1 new <workflow-name>" -ForegroundColor Yellow
            }
        }
        "compile" {
            Write-Host "⚙️ Compiling workflows..." -ForegroundColor Yellow
            gh aw compile
        }
        "run" {
            if ($WorkflowName) {
                Write-Host "🚀 Running workflow: $WorkflowName" -ForegroundColor Yellow
                gh aw run $WorkflowName
            } else {
                Write-Host "Usage: .\agentic-workflows.ps1 run <workflow-name>" -ForegroundColor Yellow
            }
        }
        "status" {
            Write-Host "📊 Workflow status:" -ForegroundColor Yellow
            gh aw status
        }
        "logs" {
            if ($WorkflowName) {
                gh aw logs $WorkflowName
            } else {
                Write-Host "Usage: .\agentic-workflows.ps1 logs <workflow-name>" -ForegroundColor Yellow
            }
        }
    }
} finally {
    Pop-Location
}
