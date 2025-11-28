#!/usr/bin/env pwsh
# =============================================================================
# Start-Aspire.ps1 - Non-blocking Aspire AppHost launcher
# =============================================================================
# This script starts the Aspire distributed application in background mode,
# suitable for AI agent invocation without blocking the terminal.
#
# Usage:
#   ./scripts/Start-Aspire.ps1              # Start in background (headless)
#   ./scripts/Start-Aspire.ps1 -Wait        # Start and wait (blocking)
#   ./scripts/Start-Aspire.ps1 -Stop        # Stop running instance
#   ./scripts/Start-Aspire.ps1 -Status      # Check status
#   ./scripts/Start-Aspire.ps1 -CpuOnly     # Force CPU/SIMD only
#
# Environment:
#   - Uses 'headless' launch profile (no browser, no interactive prompts)
#   - Connects to aspire-network for low-latency container communication
#   - Prefers NVIDIA GPU acceleration automatically when available
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Wait,
    [switch]$Stop,
    [switch]$Status,
    [switch]$CpuOnly,
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

# Calculate project root - handle both script execution and direct invocation
if ($PSScriptRoot) {
    $script:ProjectRoot = Split-Path -Parent $PSScriptRoot
} else {
    $script:ProjectRoot = Get-Location
}

# Ensure we're in the right directory
if (-not (Test-Path (Join-Path $script:ProjectRoot "Aspire-Full"))) {
    # Try current directory
    if (Test-Path "Aspire-Full") {
        $script:ProjectRoot = Get-Location
    } else {
        Write-Error "Cannot find Aspire-Full project. Run from project root or scripts directory."
        exit 1
    }
}

$script:PidFile = Join-Path $script:ProjectRoot ".aspire.pid"
$script:LogFile = Join-Path $script:ProjectRoot "TestResults" "aspire.log"

function Invoke-PipelineRunner {
    Write-Host "Running PipelineRunner (clean/restore/format/build)..." -ForegroundColor Cyan
    Push-Location $script:ProjectRoot
    $arguments = @(
        "run",
        "--project", "tools/PipelineRunner/PipelineRunner.csproj",
        "--",
        "--skip-run",
        "--configuration", "Release"
    )

    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
    Pop-Location

    if ($exitCode -ne 0) {
        Write-Host "`nPipelineRunner failed:" -ForegroundColor Red
        Write-Host ($output -join "`n")
        throw "PipelineRunner failed"
    }
}

function Get-AspireStatus {
    if (Test-Path $script:PidFile) {
        $aspirePid = Get-Content $script:PidFile -ErrorAction SilentlyContinue
        if ($aspirePid) {
            $process = Get-Process -Id $aspirePid -ErrorAction SilentlyContinue
            if ($process) {
                return @{ Running = $true; Pid = $aspirePid; Process = $process }
            }
        }
        Remove-Item $script:PidFile -Force -ErrorAction SilentlyContinue
    }
    return @{ Running = $false; Pid = $null; Process = $null }
}

function Stop-AspireApp {
    $status = Get-AspireStatus
    if ($status.Running) {
        Write-Host "Stopping Aspire (PID: $($status.Pid))..." -ForegroundColor Yellow
        Stop-Process -Id $status.Pid -Force -ErrorAction SilentlyContinue
        Remove-Item $script:PidFile -Force -ErrorAction SilentlyContinue
    }

    # Stop any orphaned dotnet processes running Aspire-Full
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*Aspire-Full*" } |
        Stop-Process -Force -ErrorAction SilentlyContinue

    # Clean up Aspire-created containers (they have random suffixes)
    Write-Host "Cleaning up Aspire containers..." -ForegroundColor Gray
    $aspireContainers = docker ps -aq --filter "name=postgres-" --filter "name=redis-" --filter "name=pgadmin-" --filter "name=rediscommander-" 2>$null
    if ($aspireContainers) {
        $aspireContainers | ForEach-Object {
            docker rm -f $_ 2>$null | Out-Null
        }
    }

    # Clean up Aspire session networks
    docker network ls --format "{{.Name}}" 2>$null |
        Where-Object { $_ -like "aspire-session-*" } |
        ForEach-Object { docker network rm $_ 2>$null | Out-Null }

    Write-Host "Aspire stopped and cleaned up." -ForegroundColor Green
    return $true
}

function Show-AspireStatus {
    $status = Get-AspireStatus
    if ($status.Running) {
        Write-Host "Aspire is RUNNING (PID: $($status.Pid))" -ForegroundColor Green

        # Try to find the dashboard URL from logs
        if (Test-Path $script:LogFile) {
            $urlMatch = Select-String -Path $script:LogFile -Pattern "Now listening on: (https?://[^\s]+)" |
                        Select-Object -Last 1
            if ($urlMatch) {
                Write-Host "Dashboard: $($urlMatch.Matches.Groups[1].Value)" -ForegroundColor Cyan
            }
        }
        return $true
    }
    Write-Host "Aspire is NOT running." -ForegroundColor Yellow
    return $false
}

function Start-AspireApp {
    param([bool]$WaitForExit)

    # Check if already running
    $status = Get-AspireStatus
    if ($status.Running) {
        Write-Host "Aspire is already running (PID: $($status.Pid))" -ForegroundColor Yellow
        return $true
    }

    # Ensure TestResults directory exists
    $testResultsDir = Join-Path $script:ProjectRoot "TestResults"
    if (-not (Test-Path $testResultsDir)) {
        New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
    }

    # ==========================================================================
    # Enable SIMD/AVX optimizations during build (always)
    # ==========================================================================
    $env:DOTNET_EnableAVX2 = "1"
    $env:DOTNET_EnableSSE41 = "1"
    $env:DOTNET_TieredPGO = "1"
    $env:DOTNET_TieredCompilation = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:DOTNET_NOLOGO = "1"

    # Prefer GPU acceleration when hardware is available unless explicitly disabled
    $gpuAvailable = $null -ne (Get-Command nvidia-smi -ErrorAction SilentlyContinue)
    $gpuRequested = -not $CpuOnly
    if ($gpuRequested -and $gpuAvailable) {
        Write-Host "GPU acceleration enabled (NVIDIA)" -ForegroundColor Magenta
        $gpuInfo = nvidia-smi --query-gpu=name,memory.free,utilization.gpu --format=csv,noheader 2>$null
        if ($gpuInfo) {
            Write-Host "GPU: $gpuInfo" -ForegroundColor Cyan
        }
        $env:CUDA_VISIBLE_DEVICES = "0"
        $env:TF_FORCE_GPU_ALLOW_GROWTH = "true"
        $env:NVIDIA_VISIBLE_DEVICES = "all"
        $env:NVIDIA_DRIVER_CAPABILITIES = "compute,utility"
    } elseif ($gpuRequested -and -not $gpuAvailable) {
        Write-Warning "GPU acceleration requested but NVIDIA utilities are not available. Falling back to CPU SIMD."
    } elseif ($CpuOnly) {
        Write-Host "CPU-only mode requested; SIMD optimizations remain enabled." -ForegroundColor Yellow
    }

    # Full clean/restore/format/build pipeline handled by PipelineRunner
    Invoke-PipelineRunner

    # Set up environment for non-interactive execution
    $env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = "true"
    $env:ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL = "http://localhost:18889"
    $env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:18889"

    Write-Host "Starting Aspire distributed application (headless mode)..." -ForegroundColor Cyan

    if ($WaitForExit) {
        # Blocking mode - run in foreground with headless profile
        Push-Location $script:ProjectRoot
        & dotnet run --project Aspire-Full --no-build --launch-profile headless --configuration Release
        Pop-Location
        return $true
    }

    # Non-blocking mode - use Start-Process for reliable background execution
    # Use headless profile to avoid browser launch and interactive prompts
    $proc = Start-Process -FilePath "dotnet" `
        -ArgumentList "run", "--project", "Aspire-Full", "--no-build", "--launch-profile", "headless", "--configuration", "Release" `
        -WorkingDirectory $script:ProjectRoot `
        -WindowStyle Hidden `
        -PassThru

    if (-not $proc) {
        Write-Host "Failed to start Aspire process" -ForegroundColor Red
        return $false
    }

    # Save PID immediately
    $proc.Id | Out-File $script:PidFile -Force
    Write-Host "Started Aspire (PID: $($proc.Id))" -ForegroundColor Gray

    # Wait for containers to start (Aspire creates containers)
    Write-Host "Waiting for Aspire to initialize..." -ForegroundColor Gray
    $startTime = Get-Date
    $containersReady = $false

    while (((Get-Date) - $startTime).TotalSeconds -lt $TimeoutSeconds) {
        Start-Sleep -Milliseconds 2000

        # Check if process exited
        if ($proc.HasExited) {
            Write-Host "Aspire process exited unexpectedly (Exit code: $($proc.ExitCode))" -ForegroundColor Red
            return $false
        }

        # Check for Aspire-created containers (postgres, redis with random suffixes)
        $containers = docker ps --format "{{.Names}}" 2>$null |
            Where-Object { $_ -match "^(postgres|redis|qdrant|pgadmin|rediscommander)-[a-z0-9]+" }
        if ($containers -and ($containers | Measure-Object).Count -ge 2) {
            $containersReady = $true
            break
        }
    }

    if ($containersReady) {
        Write-Host "`nAspire started successfully!" -ForegroundColor Green
        Write-Host "PID: $($proc.Id)" -ForegroundColor Cyan

        # Show container status
        Write-Host "`nContainers:" -ForegroundColor Cyan
        docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" 2>$null
        return $true
    }

    Write-Host "Aspire startup timed out after $TimeoutSeconds seconds." -ForegroundColor Yellow
    Write-Host "Process is still running (PID: $($proc.Id))." -ForegroundColor Gray
}

# Main execution
try {
    Push-Location $script:ProjectRoot

    if ($Stop) {
        Stop-AspireApp
    }
    elseif ($Status) {
        Show-AspireStatus
    }
    else {
        Start-AspireApp -WaitForExit:$Wait
    }
}
finally {
    Pop-Location
}
