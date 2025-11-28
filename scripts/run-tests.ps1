# Aspire-Full Test Automation Script
# Runs unit tests, E2E tests, and Aspire integration tests with full reporting
# GPU Support: Uses NVIDIA CUDA when available for accelerated test execution
# Non-blocking: Designed for CI/CD and AI agent invocation

param(
    [switch]$UnitOnly,
    [switch]$E2EOnly,
    [switch]$AspireOnly,
    [switch]$Coverage,
    [switch]$Verbose,
    [switch]$NonInteractive,
    [string]$Filter = ""
)

$ErrorActionPreference = "Continue"

# =============================================================================
# GPU and SIMD Optimization - Always enable for maximum performance
# =============================================================================
function Initialize-GpuEnvironment {
    $gpuAvailable = $null -ne (Get-Command nvidia-smi -ErrorAction SilentlyContinue)

    # Always enable .NET SIMD/AVX optimizations (CPU)
    $env:DOTNET_EnableAVX2 = "1"
    $env:DOTNET_EnableSSE41 = "1"
    $env:DOTNET_TieredPGO = "1"
    $env:DOTNET_TieredCompilation = "1"
    $env:DOTNET_ReadyToRun = "1"

    if (-not $gpuAvailable) {
        Write-Error "CUDA-capable NVIDIA GPU is required to run tests."
        exit 1
    }

    Write-Host "GPU acceleration enforced" -ForegroundColor Magenta
    $gpuInfo = nvidia-smi --query-gpu=name,memory.free,utilization.gpu --format=csv,noheader 2>$null
    Write-Host "GPU: $gpuInfo" -ForegroundColor Cyan

    $env:CUDA_VISIBLE_DEVICES = "all"
    $env:TF_FORCE_GPU_ALLOW_GROWTH = "true"
    $env:NVIDIA_VISIBLE_DEVICES = "all"
    $env:NVIDIA_DRIVER_CAPABILITIES = "compute,utility"
    $env:NVIDIA_REQUIRE_CUDA = "cuda>=12.4,driver>=535"
}

# Non-interactive mode for CI/CD and AI agents - always set for automation
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
if ($NonInteractive) {
    $env:CI = "true"
}

# Initialize GPU/SIMD environment
Initialize-GpuEnvironment

$script:TestResults = @{
    UnitTests = @{ Passed = 0; Failed = 0; Skipped = 0; Total = 0; Duration = 0 }
    E2ETests = @{ Passed = 0; Failed = 0; Skipped = 0; Total = 0; Duration = 0 }
    AspireTests = @{ Passed = 0; Failed = 0; Skipped = 0; Total = 0; Duration = 0 }
}

function Write-TestHeader {
    param([string]$Title)
    Write-Host ""
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host ""
}

function Write-TestResult {
    param(
        [string]$TestType,
        [int]$Passed,
        [int]$Failed,
        [int]$Skipped,
        [int]$Total,
        [double]$Duration
    )

    $color = if ($Failed -gt 0) { "Red" } elseif ($Skipped -eq $Total) { "Yellow" } else { "Green" }

    Write-Host ""
    Write-Host "-" * 60 -ForegroundColor Gray
    Write-Host "  $TestType Results:" -ForegroundColor $color
    Write-Host "    Total:   $Total" -ForegroundColor White
    Write-Host "    Passed:  $Passed" -ForegroundColor Green
    Write-Host "    Failed:  $Failed" -ForegroundColor $(if ($Failed -gt 0) { "Red" } else { "Gray" })
    Write-Host "    Skipped: $Skipped" -ForegroundColor $(if ($Skipped -gt 0) { "Yellow" } else { "Gray" })
    Write-Host "    Duration: $($Duration.ToString('F1'))s" -ForegroundColor Gray
    Write-Host "-" * 60 -ForegroundColor Gray
}

function ConvertFrom-TestOutput {
    param([string[]]$Output)

    $result = @{ Passed = 0; Failed = 0; Skipped = 0; Total = 0; Duration = 0 }

    foreach ($line in $Output) {
        if ($line -match "Total tests:\s*(\d+)") {
            $result.Total = [int]$Matches[1]
        }
        if ($line -match "Passed:\s*(\d+)") {
            $result.Passed = [int]$Matches[1]
        }
        if ($line -match "Failed:\s*(\d+)") {
            $result.Failed = [int]$Matches[1]
        }
        if ($line -match "Skipped:\s*(\d+)") {
            $result.Skipped = [int]$Matches[1]
        }
        if ($line -match "Total time:\s*([\d,\.]+)\s*Seconds") {
            $result.Duration = [double]($Matches[1] -replace ",", ".")
        }
        if ($line -match "duration:\s*([\d,\.]+)s") {
            $result.Duration = [double]($Matches[1] -replace ",", ".")
        }
    }

    return $result
}

function Invoke-UnitTests {
    Write-TestHeader "Running Unit Tests (xUnit)"

    $startTime = Get-Date
    $filterArg = if ($Filter) { "--filter `"$Filter`"" } else { "" }
    $coverageArg = if ($Coverage) { "--collect:`"XPlat Code Coverage`"" } else { "" }

    $output = & dotnet test Aspire-Full.Tests.Unit `
        --configuration Release `
        --no-build `
        --logger "console;verbosity=normal" `
        --results-directory "./TestResults/Unit" `
        $coverageArg `
        $filterArg 2>&1

    if ($Verbose) {
        $output | ForEach-Object { Write-Host $_ }
    }

    $result = ConvertFrom-TestOutput $output
    $result.Duration = ((Get-Date) - $startTime).TotalSeconds
    $script:TestResults.UnitTests = $result

    Write-TestResult "Unit Tests" $result.Passed $result.Failed $result.Skipped $result.Total $result.Duration

    return $result.Failed -eq 0
}

function Invoke-E2ETests {
    param([string]$Category = "")

    $categoryDisplay = if ($Category) { " ($Category)" } else { "" }
    Write-TestHeader "Running E2E Tests (NUnit + Playwright)$categoryDisplay"

    $startTime = Get-Date
    $filterArg = if ($Category) { "--filter `"TestCategory=$Category`"" } elseif ($Filter) { "--filter `"$Filter`"" } else { "" }

    $output = & dotnet test Aspire-Full.Tests.E2E `
        --configuration Release `
        --no-build `
        --logger "console;verbosity=normal" `
        --results-directory "./TestResults/E2E" `
        $filterArg 2>&1

    if ($Verbose) {
        $output | ForEach-Object { Write-Host $_ }
    }

    $result = ConvertFrom-TestOutput $output
    $result.Duration = ((Get-Date) - $startTime).TotalSeconds

    return $result
}

function Invoke-AspireIntegrationTests {
    Write-TestHeader "Running Aspire Distributed App Tests"

    Write-Host "Starting Aspire distributed application..." -ForegroundColor Yellow
    Write-Host "This will start PostgreSQL, Redis, API, and Frontend services." -ForegroundColor Gray
    Write-Host ""

    $result = Invoke-E2ETests -Category "AspireIntegration"
    $script:TestResults.AspireTests = $result

    Write-TestResult "Aspire Integration Tests" $result.Passed $result.Failed $result.Skipped $result.Total $result.Duration

    return $result.Failed -eq 0
}

function Invoke-DashboardTests {
    Write-TestHeader "Running Dashboard Tests"

    $result = Invoke-E2ETests -Category "Dashboard"
    $script:TestResults.E2ETests = $result

    Write-TestResult "Dashboard Tests" $result.Passed $result.Failed $result.Skipped $result.Total $result.Duration

    return $result.Failed -eq 0
}

function Show-FinalSummary {
    $totalPassed = $script:TestResults.UnitTests.Passed + $script:TestResults.E2ETests.Passed + $script:TestResults.AspireTests.Passed
    $totalFailed = $script:TestResults.UnitTests.Failed + $script:TestResults.E2ETests.Failed + $script:TestResults.AspireTests.Failed
    $totalSkipped = $script:TestResults.UnitTests.Skipped + $script:TestResults.E2ETests.Skipped + $script:TestResults.AspireTests.Skipped
    $totalTests = $script:TestResults.UnitTests.Total + $script:TestResults.E2ETests.Total + $script:TestResults.AspireTests.Total
    $totalDuration = $script:TestResults.UnitTests.Duration + $script:TestResults.E2ETests.Duration + $script:TestResults.AspireTests.Duration

    Write-Host ""
    Write-Host "=" * 80 -ForegroundColor $(if ($totalFailed -gt 0) { "Red" } else { "Green" })
    Write-Host "  FINAL TEST SUMMARY" -ForegroundColor $(if ($totalFailed -gt 0) { "Red" } else { "Green" })
    Write-Host "=" * 80 -ForegroundColor $(if ($totalFailed -gt 0) { "Red" } else { "Green" })
    Write-Host ""
    Write-Host "  Test Category          Passed    Failed    Skipped    Total" -ForegroundColor White
    Write-Host "  --------------------- -------- --------- ---------- --------" -ForegroundColor Gray

    $categories = @(
        @{ Name = "Unit Tests"; Data = $script:TestResults.UnitTests }
        @{ Name = "E2E/Dashboard Tests"; Data = $script:TestResults.E2ETests }
        @{ Name = "Aspire Integration"; Data = $script:TestResults.AspireTests }
    )

    foreach ($cat in $categories) {
        if ($cat.Data.Total -gt 0) {
            $passedColor = if ($cat.Data.Passed -gt 0) { "Green" } else { "Gray" }
            $failedColor = if ($cat.Data.Failed -gt 0) { "Red" } else { "Gray" }
            $skippedColor = if ($cat.Data.Skipped -gt 0) { "Yellow" } else { "Gray" }

            Write-Host ("  {0,-21}" -f $cat.Name) -NoNewline -ForegroundColor White
            Write-Host ("{0,8}" -f $cat.Data.Passed) -NoNewline -ForegroundColor $passedColor
            Write-Host ("{0,10}" -f $cat.Data.Failed) -NoNewline -ForegroundColor $failedColor
            Write-Host ("{0,11}" -f $cat.Data.Skipped) -NoNewline -ForegroundColor $skippedColor
            Write-Host ("{0,9}" -f $cat.Data.Total) -ForegroundColor White
        }
    }

    Write-Host "  --------------------- -------- --------- ---------- --------" -ForegroundColor Gray
    Write-Host ("  {0,-21}" -f "TOTAL") -NoNewline -ForegroundColor Cyan
    Write-Host ("{0,8}" -f $totalPassed) -NoNewline -ForegroundColor Green
    Write-Host ("{0,10}" -f $totalFailed) -NoNewline -ForegroundColor $(if ($totalFailed -gt 0) { "Red" } else { "Gray" })
    Write-Host ("{0,11}" -f $totalSkipped) -NoNewline -ForegroundColor $(if ($totalSkipped -gt 0) { "Yellow" } else { "Gray" })
    Write-Host ("{0,9}" -f $totalTests) -ForegroundColor White
    Write-Host ""
    Write-Host "  Total Duration: $($totalDuration.ToString('F1'))s" -ForegroundColor Gray
    Write-Host ""

    if ($totalFailed -gt 0) {
        Write-Host "  ❌ TESTS FAILED" -ForegroundColor Red
        return $false
    } else {
        Write-Host "  ✅ ALL TESTS PASSED" -ForegroundColor Green
        return $true
    }
}

# Main execution
Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
    Write-Host "║                    Aspire-Full Test Automation Suite                         ║" -ForegroundColor Magenta
    Write-Host "╚══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "  Configuration:" -ForegroundColor Gray
    Write-Host "    Unit Tests:   $(if (-not $E2EOnly -and -not $AspireOnly) { 'Enabled' } else { 'Disabled' })" -ForegroundColor Gray
    Write-Host "    E2E Tests:    $(if (-not $UnitOnly -and -not $AspireOnly) { 'Enabled' } else { 'Disabled' })" -ForegroundColor Gray
    Write-Host "    Aspire Tests: $(if (-not $UnitOnly -and -not $E2EOnly) { 'Enabled' } else { 'Disabled' })" -ForegroundColor Gray
    Write-Host "    Coverage:     $(if ($Coverage) { 'Enabled' } else { 'Disabled' })" -ForegroundColor Gray
    Write-Host ""

    # Build solution first
    Write-Host "Building solution..." -ForegroundColor Yellow
    $buildOutput = & dotnet build --configuration Release 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        $buildOutput | ForEach-Object { Write-Host $_ }
        exit 1
    }
    Write-Host "Build succeeded." -ForegroundColor Green

    $script:allTestsPassed = $true

    # Run Unit Tests
    if (-not $E2EOnly -and -not $AspireOnly) {
        if (-not (Invoke-UnitTests)) { $script:allTestsPassed = $false }
    }

    # Run E2E/Dashboard Tests
    if (-not $UnitOnly -and -not $AspireOnly) {
        if (-not (Invoke-DashboardTests)) { $script:allTestsPassed = $false }
    }

    # Run Aspire Integration Tests
    if (-not $UnitOnly -and -not $E2EOnly) {
        if (-not (Invoke-AspireIntegrationTests)) { $script:allTestsPassed = $false }
    }

    # Show final summary (uses $script:allTestsPassed internally via test results)
    $success = Show-FinalSummary

    exit $(if ($success) { 0 } else { 1 })
}
finally {
    Pop-Location
}
