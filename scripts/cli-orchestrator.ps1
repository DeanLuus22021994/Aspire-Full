#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Aspire-Full CLI Orchestrator - Bleeding-edge .NET 10 development automation
.DESCRIPTION
    Comprehensive CLI ecosystem for AI-optimized development with enhanced UX,
    automated testing, and intelligent orchestration capabilities.
.NOTES
    Author: Aspire-Full Team
    Version: 2.0.0
    Requires: .NET 10, PowerShell 7+
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet('build', 'test', 'watch', 'analyze', 'clean', 'restore', 'publish', 'doctor', 'ai', 'profile', 'help')]
    [string]$Command = 'help',

    [Parameter(Position = 1, ValueFromRemainingArguments)]
    [string[]]$Arguments,

    [switch]$Parallel,
    [switch]$Coverage,
    [switch]$NoRestore
)

# ============================================================================
# Configuration
# ============================================================================
$script:Config = @{
    WorkspaceRoot = $PSScriptRoot | Split-Path -Parent
    SolutionFile = 'Aspire-Full.slnx'
    TestSettings = 'Tests/tests.runsettings'
    ArtifactsPath = 'artifacts'
    LogsPath = 'artifacts/logs'
    Colors = @{
        Success = 'Green'
        Error = 'Red'
        Warning = 'Yellow'
        Info = 'Cyan'
        Muted = 'DarkGray'
    }
}

# ============================================================================
# UX Helpers
# ============================================================================

function Write-Banner {
    param([string]$Title)
    $width = 70
    $line = '=' * $width
    Write-Host ""
    Write-Host $line -ForegroundColor $Config.Colors.Info
    Write-Host " $Title" -ForegroundColor White
    Write-Host $line -ForegroundColor $Config.Colors.Info
}

function Write-Step {
    param([string]$Message, [int]$Step, [int]$Total)
    $prefix = "[$Step/$Total]"
    Write-Host "$prefix " -ForegroundColor $Config.Colors.Info -NoNewline
    Write-Host $Message -ForegroundColor White
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] " -ForegroundColor $Config.Colors.Success -NoNewline
    Write-Host $Message -ForegroundColor $Config.Colors.Success
}

function Write-Failure {
    param([string]$Message)
    Write-Host "[FAIL] " -ForegroundColor $Config.Colors.Error -NoNewline
    Write-Host $Message -ForegroundColor $Config.Colors.Error
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] " -ForegroundColor $Config.Colors.Warning -NoNewline
    Write-Host $Message -ForegroundColor $Config.Colors.Warning
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] " -ForegroundColor $Config.Colors.Info -NoNewline
    Write-Host $Message -ForegroundColor $Config.Colors.Muted
}

# ============================================================================
# Core Commands
# ============================================================================

function Invoke-Build {
    param([switch]$Release, [switch]$NoRestore)

    Write-Banner "Building Aspire-Full Solution"

    Push-Location $Config.WorkspaceRoot
    try {
        $buildArgs = @(
            'build'
            $Config.SolutionFile
            '--verbosity', 'minimal'
        )

        if ($Release) { $buildArgs += '--configuration', 'Release' }
        if ($NoRestore) { $buildArgs += '--no-restore' }

        # Enable binary logging for analysis
        $logPath = Join-Path $Config.LogsPath "build-$(Get-Date -Format 'yyyyMMdd-HHmmss').binlog"
        New-Item -ItemType Directory -Force -Path $Config.LogsPath | Out-Null
        $buildArgs += "-bl:$logPath"

        Write-Step "Executing dotnet build with GPU acceleration" 1 2

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        & dotnet @buildArgs 2>&1
        $sw.Stop()

        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0) {
            Write-Success "Build completed in $($sw.Elapsed.TotalSeconds.ToString('F2'))s"
            Write-Info "Binary log: $logPath"
        } else {
            Write-Failure "Build failed with exit code $exitCode"
            return $false
        }

        Write-Step "Verifying build artifacts" 2 2
        $assemblies = Get-ChildItem -Path $Config.ArtifactsPath -Filter '*.dll' -Recurse -ErrorAction SilentlyContinue | Measure-Object
        Write-Info "Generated $($assemblies.Count) assemblies"

        return $true
    }
    finally {
        Pop-Location
    }
}

function Invoke-Test {
    param(
        [switch]$Coverage,
        [switch]$Unit,
        [switch]$E2E,
        [switch]$Integration,
        [string]$Filter
    )

    Write-Banner "Running Tests"

    Push-Location $Config.WorkspaceRoot
    try {
        $testArgs = @(
            'test'
            '--settings', $Config.TestSettings
            '--verbosity', 'normal'
            '--blame-hang-timeout', '60s'
            '--results-directory', 'artifacts/test-results'
        )

        if ($Coverage) {
            $testArgs += '--collect:"XPlat Code Coverage"'
        }

        if ($Filter) {
            $testArgs += '--filter', $Filter
        }

        # Filter by project type
        if ($Unit) {
            $testArgs += '--filter', 'FullyQualifiedName~Tests.Unit'
        } elseif ($E2E) {
            $testArgs += '--filter', 'Category=E2E|Category=Integration'
        } elseif ($Integration) {
            $testArgs += '--filter', 'Category=Integration'
        }

        Write-Step "Executing test suite" 1 3

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $result = & dotnet @testArgs 2>&1
        $sw.Stop()

        Write-Step "Analyzing test results" 2 3

        # Output results
        $result | ForEach-Object { Write-Host $_ }

        Write-Step "Test Summary" 3 3
        Write-Host ""
        Write-Host "  Duration: $($sw.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor $Config.Colors.Muted
        Write-Host ""

        if ($LASTEXITCODE -eq 0) {
            Write-Success "All tests passed!"
            return $true
        } else {
            Write-Failure "Test run completed with failures"
            return $false
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-Watch {
    param([string]$Project)

    Write-Banner "Starting Watch Mode"

    Push-Location $Config.WorkspaceRoot
    try {
        $watchArgs = @('watch')

        if ($Project) {
            $watchArgs += '--project', $Project
        }

        $watchArgs += 'run'

        Write-Info "Press Ctrl+C to stop watching"
        Write-Host ""

        & dotnet @watchArgs
    }
    finally {
        Pop-Location
    }
}

function Invoke-Analyze {
    Write-Banner "Code Analysis & Quality Check"

    Push-Location $Config.WorkspaceRoot
    try {
        $steps = 4
        $currentStep = 1

        # Step 1: Format check
        Write-Step "Checking code formatting" $currentStep $steps
        & dotnet format --verify-no-changes --verbosity diagnostic 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Code formatting: OK"
        } else {
            Write-Warn "Code formatting issues detected - run 'dotnet format' to fix"
        }
        $currentStep++

        # Step 2: Build with analyzers
        Write-Step "Running Roslyn analyzers" $currentStep $steps
        $analyzeResult = & dotnet build $Config.SolutionFile --no-restore /p:ReportAnalyzer=true 2>&1
        $analyzerWarnings = $analyzeResult | Select-String -Pattern 'warning [A-Z]+\d+' | Measure-Object
        if ($analyzerWarnings.Count -eq 0) {
            Write-Success "Analyzers: No issues found"
        } else {
            Write-Warn "Analyzers: $($analyzerWarnings.Count) warnings"
        }
        $currentStep++

        # Step 3: Security scan
        Write-Step "Scanning for vulnerabilities" $currentStep $steps
        $vulnResult = & dotnet list package --vulnerable --include-transitive 2>&1
        $vulnCount = ($vulnResult | Select-String -Pattern 'has the following vulnerable packages' | Measure-Object).Count
        if ($vulnCount -eq 0) {
            Write-Success "Security: No vulnerable packages"
        } else {
            Write-Failure "Security: $vulnCount projects have vulnerable packages"
        }
        $currentStep++

        # Step 4: Outdated packages
        Write-Step "Checking for outdated packages" $currentStep $steps
        $outdatedResult = & dotnet list package --outdated 2>&1
        $outdatedCount = ($outdatedResult | Select-String -Pattern '>' | Measure-Object).Count
        if ($outdatedCount -eq 0) {
            Write-Success "Dependencies: All up to date"
        } else {
            Write-Info "Dependencies: $outdatedCount packages can be updated"
        }

        Write-Host ""
        Write-Success "Analysis complete"
        return $true
    }
    finally {
        Pop-Location
    }
}

function Invoke-Clean {
    param([switch]$Deep)

    Write-Banner "Cleaning Build Artifacts"

    Push-Location $Config.WorkspaceRoot
    try {
        Write-Step "Running dotnet clean" 1 2
        & dotnet clean $Config.SolutionFile --verbosity minimal 2>&1 | Out-Null

        Write-Step "Removing artifacts" 2 2

        $pathsToRemove = @(
            $Config.ArtifactsPath
        )

        if ($Deep) {
            $pathsToRemove += @(
                'bin'
                'obj'
                '.vs'
                'node_modules'
                '__pycache__'
                '.mypy_cache'
                '.pytest_cache'
                'TestResults'
            )
        }

        foreach ($path in $pathsToRemove) {
            $fullPath = Join-Path $Config.WorkspaceRoot $path
            if (Test-Path $fullPath) {
                Remove-Item -Path $fullPath -Recurse -Force -ErrorAction SilentlyContinue
                Write-Info "Removed: $path"
            }
        }

        if ($Deep) {
            # Remove all bin/obj recursively
            Get-ChildItem -Path $Config.WorkspaceRoot -Include 'bin', 'obj' -Recurse -Directory -ErrorAction SilentlyContinue |
                Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            Write-Info "Removed all bin/obj directories"
        }

        Write-Success "Clean complete"
        return $true
    }
    finally {
        Pop-Location
    }
}

function Invoke-Doctor {
    Write-Banner "System Health Check"

    $checks = @(
        @{ Name = '.NET SDK'; Command = { dotnet --version } }
        @{ Name = 'Docker'; Command = { docker --version } }
        @{ Name = 'Node.js'; Command = { node --version } }
        @{ Name = 'Python'; Command = { python --version } }
        @{ Name = 'uv'; Command = { uv --version } }
        @{ Name = 'Git'; Command = { git --version } }
        @{ Name = 'NVIDIA Driver'; Command = { nvidia-smi --query-gpu=driver_version --format=csv,noheader 2>$null } }
    )

    foreach ($check in $checks) {
        try {
            $version = & $check.Command 2>&1
            if ($LASTEXITCODE -eq 0 -and $version) {
                Write-Success "$($check.Name): $($version.ToString().Trim())"
            } else {
                Write-Warn "$($check.Name): Not available"
            }
        } catch {
            Write-Warn "$($check.Name): Not found"
        }
    }

    Write-Host ""

    # Check GPU acceleration
    Write-Info "Checking GPU acceleration..."
    $gpuPath1 = "C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe"
    $gpuPath2 = "$env:SystemRoot\System32\nvidia-smi.exe"
    $gpuEnabled = (Test-Path $gpuPath1) -or (Test-Path $gpuPath2)

    if ($gpuEnabled) {
        Write-Success "GPU acceleration: Available"
    } else {
        Write-Warn "GPU acceleration: Not available (CPU fallback)"
    }

    # Check solution health
    Write-Host ""
    Write-Info "Checking solution..."

    Push-Location $Config.WorkspaceRoot
    try {
        & dotnet restore $Config.SolutionFile --verbosity quiet 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Package restore: OK"
        } else {
            Write-Failure "Package restore: Failed"
        }
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    Write-Success "Health check complete"
}

function Invoke-Profile {
    Write-Banner "Performance Profiling"

    Push-Location $Config.WorkspaceRoot
    try {
        Write-Step "Generating build metrics" 1 2

        # Create detailed build log
        $logPath = Join-Path $Config.LogsPath "profile-$(Get-Date -Format 'yyyyMMdd-HHmmss').binlog"
        New-Item -ItemType Directory -Force -Path $Config.LogsPath | Out-Null

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        & dotnet build $Config.SolutionFile --no-incremental "-bl:$logPath" 2>&1 | Out-Null
        $sw.Stop()

        Write-Step "Analyzing performance data" 2 2

        Write-Host ""
        Write-Info "Build Performance Summary:"
        Write-Host "  Total build time: $($sw.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor White
        Write-Host "  Binary log: $logPath" -ForegroundColor $Config.Colors.Muted
        Write-Host ""
        Write-Info "Use MSBuild Structured Log Viewer to analyze the binary log"

        return $true
    }
    finally {
        Pop-Location
    }
}

function Invoke-AI {
    param([string[]]$AIArgs)

    Write-Banner "AI Orchestration"

    Write-Info "AI orchestration capabilities:"
    Write-Host "  - Registry analysis (self-enhancement)" -ForegroundColor White
    Write-Host "  - Code profiling (pain point detection)" -ForegroundColor White
    Write-Host "  - Model runner (portable inference)" -ForegroundColor White
    Write-Host ""

    if ($AIArgs -contains 'analyze') {
        Push-Location $Config.WorkspaceRoot
        try {
            Write-Step "Running registry analyzer" 1 1
            $pythonPath = Join-Path $Config.WorkspaceRoot "AI/Aspire-Full.Python/python-agents/.venv/Scripts/python.exe"
            $analyzerPath = Join-Path $Config.WorkspaceRoot "Infra/Aspire-Full.DevContainer/Scripts/registry_analyzer.py"

            if (Test-Path $pythonPath) {
                & $pythonPath $analyzerPath
            } else {
                & python $analyzerPath
            }
        }
        finally {
            Pop-Location
        }
    } else {
        Write-Info "Usage: cli-orchestrator ai analyze"
    }
}

function Show-Help {
    Write-Banner "Aspire-Full CLI Orchestrator"

    Write-Host ""
    Write-Host "USAGE:" -ForegroundColor $Config.Colors.Info
    Write-Host "  ./scripts/cli-orchestrator.ps1 <command> [options]" -ForegroundColor White
    Write-Host ""
    Write-Host "COMMANDS:" -ForegroundColor $Config.Colors.Info
    Write-Host "  build      Build the solution" -ForegroundColor White
    Write-Host "  test       Run tests (--unit, --e2e, --coverage)" -ForegroundColor White
    Write-Host "  watch      Start watch mode for hot reload" -ForegroundColor White
    Write-Host "  analyze    Run code analysis and security scans" -ForegroundColor White
    Write-Host "  clean      Clean build artifacts (--deep for full clean)" -ForegroundColor White
    Write-Host "  restore    Restore NuGet packages" -ForegroundColor White
    Write-Host "  doctor     Check system health and dependencies" -ForegroundColor White
    Write-Host "  ai         AI orchestration commands" -ForegroundColor White
    Write-Host "  profile    Performance profiling and metrics" -ForegroundColor White
    Write-Host "  help       Show this help message" -ForegroundColor White
    Write-Host ""
    Write-Host "OPTIONS:" -ForegroundColor $Config.Colors.Info
    Write-Host "  -Parallel  Enable parallel execution" -ForegroundColor White
    Write-Host "  -Coverage  Collect code coverage" -ForegroundColor White
    Write-Host "  -NoRestore Skip package restore" -ForegroundColor White
    Write-Host ""
    Write-Host "EXAMPLES:" -ForegroundColor $Config.Colors.Info
    Write-Host "  ./scripts/cli-orchestrator.ps1 build" -ForegroundColor $Config.Colors.Muted
    Write-Host "  ./scripts/cli-orchestrator.ps1 test -Coverage" -ForegroundColor $Config.Colors.Muted
    Write-Host "  ./scripts/cli-orchestrator.ps1 clean -Deep" -ForegroundColor $Config.Colors.Muted
    Write-Host "  ./scripts/cli-orchestrator.ps1 doctor" -ForegroundColor $Config.Colors.Muted
    Write-Host ""
}

# ============================================================================
# Main Entry Point
# ============================================================================

$ErrorActionPreference = 'Stop'

switch ($Command) {
    'build' {
        $params = @{}
        if ($Arguments -contains '-Release') { $params['Release'] = $true }
        if ($NoRestore) { $params['NoRestore'] = $true }
        Invoke-Build @params
    }
    'test' {
        $params = @{}
        if ($Coverage) { $params['Coverage'] = $true }
        if ($Arguments -contains '-Unit') { $params['Unit'] = $true }
        if ($Arguments -contains '-E2E') { $params['E2E'] = $true }
        if ($Arguments -contains '-Integration') { $params['Integration'] = $true }
        Invoke-Test @params
    }
    'watch' {
        Invoke-Watch -Project ($Arguments | Select-Object -First 1)
    }
    'analyze' {
        Invoke-Analyze
    }
    'clean' {
        Invoke-Clean -Deep:($Arguments -contains '-Deep')
    }
    'restore' {
        Push-Location $Config.WorkspaceRoot
        & dotnet restore $Config.SolutionFile
        Pop-Location
    }
    'doctor' {
        Invoke-Doctor
    }
    'ai' {
        Invoke-AI -AIArgs $Arguments
    }
    'profile' {
        Invoke-Profile
    }
    'help' {
        Show-Help
    }
    default {
        Show-Help
    }
}
