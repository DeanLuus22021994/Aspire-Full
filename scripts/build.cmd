@echo off
REM =============================================================================
REM build.cmd - GPU-accelerated build script
REM =============================================================================
REM Enables NVIDIA GPU compute and SIMD optimizations during build
REM =============================================================================

cd /d c:\Users\Dean\source\Aspire-Full

REM Enable GPU compute if available
where nvidia-smi >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [GPU] NVIDIA GPU detected - enabling compute acceleration
    set CUDA_VISIBLE_DEVICES=0
    set NVIDIA_VISIBLE_DEVICES=all
    set TF_FORCE_GPU_ALLOW_GROWTH=true
)

REM Enable .NET SIMD/AVX optimizations
set DOTNET_EnableAVX2=1
set DOTNET_EnableSSE41=1
set DOTNET_TieredPGO=1
set DOTNET_ReadyToRun=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_NOLOGO=1

echo [BUILD] Starting GPU-accelerated build...
dotnet build --configuration Release --verbosity minimal

echo.
echo Build exit code: %ERRORLEVEL%
