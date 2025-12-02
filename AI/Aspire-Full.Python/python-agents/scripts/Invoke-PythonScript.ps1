<#
.SYNOPSIS
    Unified Python script runner for aspire_agents with proper environment isolation.

.DESCRIPTION
    Provides a single entry point to run Python scripts in the python-agents project
    with automatic virtual environment activation and path configuration.

    Follows SOLID principles:
    - Single Responsibility: Each script has one purpose
    - Open/Closed: Extensible via ScriptName parameter
    - Liskov Substitution: All scripts run through same interface
    - Interface Segregation: Minimal required parameters
    - Dependency Inversion: Abstracts Python execution details

.PARAMETER ScriptName
    The name of the script to run. Valid values:
    - abstract_dependency_report: Scan mypy cache and generate vendor abstractions
    - download_models: Download ML models for local compute
    - generate_dependency_report: Generate package dependency report
    - run_agent: Run an agent with config and input

.PARAMETER Config
    Configuration file path (required for run_agent)

.PARAMETER Input
    Input message (required for run_agent)

.PARAMETER Packages
    Comma-separated package list (optional for abstract_dependency_report)

.PARAMETER OutputDir
    Output directory path (optional for abstract_dependency_report)

.PARAMETER PassThru
    Return the script output instead of writing to host

.EXAMPLE
    .\Invoke-PythonScript.ps1 -ScriptName abstract_dependency_report

.EXAMPLE
    .\Invoke-PythonScript.ps1 -ScriptName download_models

.EXAMPLE
    .\Invoke-PythonScript.ps1 -ScriptName run_agent -Config "config.yaml" -Input "Hello"

.NOTES
    Requires Python 3.15+ with free-threading support and uv package manager.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet(
        "abstract_dependency_report",
        "download_models",
        "generate_dependency_report",
        "run_agent"
    )]
    [string]$ScriptName,

    [Parameter()]
    [string]$Config,

    [Parameter()]
    [string]$Input,

    [Parameter()]
    [string]$Packages,

    [Parameter()]
    [string]$OutputDir,

    [Parameter()]
    [switch]$PassThru
)

$ErrorActionPreference = "Stop"

# ============================================================================
# Path Configuration
# ============================================================================

$ScriptDir = $PSScriptRoot
$ProjectRoot = Split-Path -Parent $ScriptDir
$SrcDir = Join-Path $ProjectRoot "src"
$VenvDir = Join-Path $ProjectRoot ".venv"
$CacheDir = Join-Path $ProjectRoot ".cache"

# ============================================================================
# Environment Validation
# ============================================================================

function Test-PythonEnvironment {
    <#
    .SYNOPSIS
        Validates the Python environment is properly configured.
    #>

    # Check uv is available
    if (-not (Get-Command "uv" -ErrorAction SilentlyContinue)) {
        throw "uv package manager not found. Install from: https://docs.astral.sh/uv/"
    }

    # Check venv exists
    if (-not (Test-Path $VenvDir)) {
        Write-Host "Virtual environment not found. Creating..." -ForegroundColor Yellow
        Push-Location $ProjectRoot
        try {
            uv venv --python 3.15
        }
        finally {
            Pop-Location
        }
    }

    # Verify Python version
    $pythonVersion = uv run python --version 2>&1
    if ($pythonVersion -notmatch "3\.15") {
        Write-Warning "Expected Python 3.15, got: $pythonVersion"
    }

    return $true
}

# ============================================================================
# Script Execution Functions
# ============================================================================

function Invoke-AbstractDependencyReport {
    <#
    .SYNOPSIS
        Runs the abstract_dependency_report.py script.
    #>
    param(
        [string]$Packages,
        [string]$OutputDir
    )

    $scriptPath = Join-Path $ScriptDir "abstract_dependency_report.py"
    $args = @()

    if ($Packages) {
        $args += "--packages", $Packages
    }

    if ($OutputDir) {
        $args += "--output-dir", $OutputDir
    }

    $args += "--base-dir", $ProjectRoot

    Write-Host "Running abstract_dependency_report..." -ForegroundColor Cyan
    Write-Host "  Cache Dir: $CacheDir" -ForegroundColor DarkGray
    Write-Host "  Vendor Dir: $SrcDir\aspire_agents\_vendor" -ForegroundColor DarkGray

    Push-Location $ProjectRoot
    try {
        if ($args.Count -gt 0) {
            uv run python $scriptPath @args
        }
        else {
            uv run python $scriptPath --base-dir $ProjectRoot
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-DownloadModels {
    <#
    .SYNOPSIS
        Runs the download_models.py script.
    #>

    $scriptPath = Join-Path $ScriptDir "download_models.py"

    Write-Host "Running download_models..." -ForegroundColor Cyan
    Write-Host "  Models will be cached in HuggingFace cache" -ForegroundColor DarkGray

    Push-Location $ProjectRoot
    try {
        uv run python $scriptPath
    }
    finally {
        Pop-Location
    }
}

function Invoke-GenerateDependencyReport {
    <#
    .SYNOPSIS
        Runs the generate_dependency_report.py script.
    #>

    $scriptPath = Join-Path $ScriptDir "generate_dependency_report.py"

    Write-Host "Running generate_dependency_report..." -ForegroundColor Cyan

    Push-Location $ProjectRoot
    try {
        uv run python $scriptPath
    }
    finally {
        Pop-Location
    }
}

function Invoke-RunAgent {
    <#
    .SYNOPSIS
        Runs an agent with the specified config and input.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Config,

        [Parameter(Mandatory = $true)]
        [string]$Input
    )

    Write-Host "Running agent..." -ForegroundColor Cyan
    Write-Host "  Config: $Config" -ForegroundColor DarkGray
    Write-Host "  Input: $Input" -ForegroundColor DarkGray

    Push-Location $ProjectRoot
    try {
        uv run python -m aspire_agents.cli run --config $Config --input $Input
    }
    finally {
        Pop-Location
    }
}

# ============================================================================
# Main Execution
# ============================================================================

# Validate environment
$null = Test-PythonEnvironment

# Dispatch to appropriate function
switch ($ScriptName) {
    "abstract_dependency_report" {
        Invoke-AbstractDependencyReport -Packages $Packages -OutputDir $OutputDir
    }
    "download_models" {
        Invoke-DownloadModels
    }
    "generate_dependency_report" {
        Invoke-GenerateDependencyReport
    }
    "run_agent" {
        if (-not $Config) {
            throw "Config parameter is required for run_agent"
        }
        if (-not $Input) {
            throw "Input parameter is required for run_agent"
        }
        Invoke-RunAgent -Config $Config -Input $Input
    }
}

Write-Host "`nScript completed successfully." -ForegroundColor Green
