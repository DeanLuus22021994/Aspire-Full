<#
.SYNOPSIS
    Manages the Python cache directories for the python-agents project.

.DESCRIPTION
    Provides utilities to clean, organize, and inspect cache directories
    including mypy cache, pytest cache, and __pycache__ directories.

    Optimized for Python 3.15 free-threading environment.

.PARAMETER Action
    The action to perform:
    - clean: Remove all cache directories
    - clean-pycache: Remove only __pycache__ directories
    - clean-mypy: Remove mypy cache
    - clean-pytest: Remove pytest cache
    - status: Show cache directory status and sizes
    - rebuild-mypy: Clean and rebuild mypy cache

.PARAMETER Force
    Skip confirmation prompts

.EXAMPLE
    .\Invoke-CacheManagement.ps1 -Action status

.EXAMPLE
    .\Invoke-CacheManagement.ps1 -Action clean -Force

.EXAMPLE
    .\Invoke-CacheManagement.ps1 -Action rebuild-mypy
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet(
        "clean",
        "clean-pycache",
        "clean-mypy",
        "clean-pytest",
        "status",
        "rebuild-mypy"
    )]
    [string]$Action,

    [Parameter()]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# ============================================================================
# Path Configuration
# ============================================================================

$ScriptDir = $PSScriptRoot
$ProjectRoot = Split-Path -Parent $ScriptDir
$SrcDir = Join-Path $ProjectRoot "src"
$CacheDir = Join-Path $ProjectRoot ".cache"
$MypyCacheDir = Join-Path $CacheDir "mypy"
$PytestCacheDir = Join-Path $ProjectRoot ".pytest_cache"

# ============================================================================
# Helper Functions
# ============================================================================

function Get-DirectorySize {
    <#
    .SYNOPSIS
        Gets the size of a directory in human-readable format.
    #>
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return "N/A"
    }

    $size = (Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue |
             Measure-Object -Property Length -Sum).Sum

    if ($null -eq $size -or $size -eq 0) {
        return "0 B"
    }
    elseif ($size -lt 1KB) {
        return "$size B"
    }
    elseif ($size -lt 1MB) {
        return "{0:N2} KB" -f ($size / 1KB)
    }
    elseif ($size -lt 1GB) {
        return "{0:N2} MB" -f ($size / 1MB)
    }
    else {
        return "{0:N2} GB" -f ($size / 1GB)
    }
}

function Get-FileCount {
    <#
    .SYNOPSIS
        Gets the number of files in a directory.
    #>
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return 0
    }

    return (Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue).Count
}

function Remove-CacheDirectory {
    <#
    .SYNOPSIS
        Removes a cache directory with confirmation.
    #>
    param(
        [string]$Path,
        [string]$Description,
        [switch]$Force
    )

    if (-not (Test-Path $Path)) {
        Write-Host "  $Description not found: $Path" -ForegroundColor DarkGray
        return
    }

    $size = Get-DirectorySize $Path
    $count = Get-FileCount $Path

    if (-not $Force) {
        $confirm = Read-Host "Remove $Description ($count files, $size)? [y/N]"
        if ($confirm -ne "y" -and $confirm -ne "Y") {
            Write-Host "  Skipped $Description" -ForegroundColor Yellow
            return
        }
    }

    try {
        Remove-Item -Path $Path -Recurse -Force -ErrorAction Stop
        Write-Host "  Removed $Description ($count files, $size)" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to remove $Description`: $_"
    }
}

function Find-PycacheDirectories {
    <#
    .SYNOPSIS
        Finds all __pycache__ directories under the project.
    #>

    $pycacheDirs = Get-ChildItem -Path $ProjectRoot -Directory -Recurse -Filter "__pycache__" -ErrorAction SilentlyContinue
    return $pycacheDirs
}

# ============================================================================
# Action Functions
# ============================================================================

function Show-CacheStatus {
    <#
    .SYNOPSIS
        Shows the status of all cache directories.
    #>

    Write-Host "`n=== Python Cache Status ===" -ForegroundColor Cyan
    Write-Host "Project: $ProjectRoot`n" -ForegroundColor DarkGray

    # Mypy cache
    Write-Host "Mypy Cache:" -ForegroundColor White
    if (Test-Path $MypyCacheDir) {
        $versions = Get-ChildItem -Path $MypyCacheDir -Directory -ErrorAction SilentlyContinue
        foreach ($ver in $versions) {
            $size = Get-DirectorySize $ver.FullName
            $count = Get-FileCount $ver.FullName
            Write-Host "  Python $($ver.Name): $count files, $size" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "  Not found" -ForegroundColor DarkGray
    }

    # Pytest cache
    Write-Host "`nPytest Cache:" -ForegroundColor White
    if (Test-Path $PytestCacheDir) {
        $size = Get-DirectorySize $PytestCacheDir
        $count = Get-FileCount $PytestCacheDir
        Write-Host "  $count files, $size" -ForegroundColor Gray
    }
    else {
        Write-Host "  Not found" -ForegroundColor DarkGray
    }

    # __pycache__ directories
    Write-Host "`n__pycache__ Directories:" -ForegroundColor White
    $pycacheDirs = Find-PycacheDirectories
    if ($pycacheDirs.Count -gt 0) {
        $totalSize = 0
        $totalFiles = 0
        foreach ($dir in $pycacheDirs) {
            $totalFiles += Get-FileCount $dir.FullName
            $bytes = (Get-ChildItem -Path $dir.FullName -Recurse -File -ErrorAction SilentlyContinue |
                      Measure-Object -Property Length -Sum).Sum
            if ($bytes) { $totalSize += $bytes }
        }
        Write-Host "  $($pycacheDirs.Count) directories, $totalFiles files" -ForegroundColor Gray

        # Show locations
        foreach ($dir in $pycacheDirs | Select-Object -First 5) {
            $relativePath = $dir.FullName.Replace($ProjectRoot, ".")
            Write-Host "    $relativePath" -ForegroundColor DarkGray
        }
        if ($pycacheDirs.Count -gt 5) {
            Write-Host "    ... and $($pycacheDirs.Count - 5) more" -ForegroundColor DarkGray
        }
    }
    else {
        Write-Host "  None found" -ForegroundColor DarkGray
    }

    Write-Host ""
}

function Clear-AllCaches {
    <#
    .SYNOPSIS
        Clears all cache directories.
    #>
    param([switch]$Force)

    Write-Host "`n=== Cleaning All Caches ===" -ForegroundColor Cyan

    # Clean __pycache__ directories
    $pycacheDirs = Find-PycacheDirectories
    if ($pycacheDirs.Count -gt 0) {
        if (-not $Force) {
            $confirm = Read-Host "Remove $($pycacheDirs.Count) __pycache__ directories? [y/N]"
            if ($confirm -ne "y" -and $confirm -ne "Y") {
                Write-Host "  Skipped __pycache__ directories" -ForegroundColor Yellow
            }
            else {
                foreach ($dir in $pycacheDirs) {
                    Remove-Item -Path $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
                }
                Write-Host "  Removed $($pycacheDirs.Count) __pycache__ directories" -ForegroundColor Green
            }
        }
        else {
            foreach ($dir in $pycacheDirs) {
                Remove-Item -Path $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
            Write-Host "  Removed $($pycacheDirs.Count) __pycache__ directories" -ForegroundColor Green
        }
    }

    # Clean mypy cache
    Remove-CacheDirectory -Path $MypyCacheDir -Description "Mypy cache" -Force:$Force

    # Clean pytest cache
    Remove-CacheDirectory -Path $PytestCacheDir -Description "Pytest cache" -Force:$Force

    Write-Host "`nCache cleanup complete." -ForegroundColor Green
}

function Clear-PycacheOnly {
    <#
    .SYNOPSIS
        Clears only __pycache__ directories.
    #>
    param([switch]$Force)

    Write-Host "`n=== Cleaning __pycache__ Directories ===" -ForegroundColor Cyan

    $pycacheDirs = Find-PycacheDirectories
    if ($pycacheDirs.Count -eq 0) {
        Write-Host "No __pycache__ directories found." -ForegroundColor DarkGray
        return
    }

    if (-not $Force) {
        $confirm = Read-Host "Remove $($pycacheDirs.Count) __pycache__ directories? [y/N]"
        if ($confirm -ne "y" -and $confirm -ne "Y") {
            Write-Host "Cancelled." -ForegroundColor Yellow
            return
        }
    }

    foreach ($dir in $pycacheDirs) {
        $relativePath = $dir.FullName.Replace($ProjectRoot, ".")
        Remove-Item -Path $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Removed: $relativePath" -ForegroundColor Gray
    }

    Write-Host "`nRemoved $($pycacheDirs.Count) __pycache__ directories." -ForegroundColor Green
}

function Clear-MypyCache {
    <#
    .SYNOPSIS
        Clears the mypy cache.
    #>
    param([switch]$Force)

    Write-Host "`n=== Cleaning Mypy Cache ===" -ForegroundColor Cyan
    Remove-CacheDirectory -Path $MypyCacheDir -Description "Mypy cache" -Force:$Force
}

function Clear-PytestCache {
    <#
    .SYNOPSIS
        Clears the pytest cache.
    #>
    param([switch]$Force)

    Write-Host "`n=== Cleaning Pytest Cache ===" -ForegroundColor Cyan
    Remove-CacheDirectory -Path $PytestCacheDir -Description "Pytest cache" -Force:$Force
}

function Invoke-RebuildMypyCache {
    <#
    .SYNOPSIS
        Cleans and rebuilds the mypy cache.
    #>

    Write-Host "`n=== Rebuilding Mypy Cache ===" -ForegroundColor Cyan

    # Clean existing cache
    if (Test-Path $MypyCacheDir) {
        Remove-Item -Path $MypyCacheDir -Recurse -Force
        Write-Host "  Removed existing mypy cache" -ForegroundColor Gray
    }

    # Run mypy to rebuild cache
    Write-Host "  Running mypy to rebuild cache..." -ForegroundColor Gray

    Push-Location $ProjectRoot
    try {
        # Run mypy on the src directory
        uv run mypy $SrcDir --cache-dir $CacheDir/mypy 2>&1 | Out-Null

        if (Test-Path $MypyCacheDir) {
            $size = Get-DirectorySize $MypyCacheDir
            $count = Get-FileCount $MypyCacheDir
            Write-Host "  Rebuilt mypy cache: $count files, $size" -ForegroundColor Green
        }
        else {
            Write-Host "  Mypy cache was not created (check mypy configuration)" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Warning "Mypy rebuild failed: $_"
    }
    finally {
        Pop-Location
    }
}

# ============================================================================
# Main Execution
# ============================================================================

switch ($Action) {
    "status" {
        Show-CacheStatus
    }
    "clean" {
        Clear-AllCaches -Force:$Force
    }
    "clean-pycache" {
        Clear-PycacheOnly -Force:$Force
    }
    "clean-mypy" {
        Clear-MypyCache -Force:$Force
    }
    "clean-pytest" {
        Clear-PytestCache -Force:$Force
    }
    "rebuild-mypy" {
        Invoke-RebuildMypyCache
    }
}
