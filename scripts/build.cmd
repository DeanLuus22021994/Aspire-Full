@echo off
REM =============================================================================
REM build.cmd - GPU-accelerated build script
REM =============================================================================
REM Enables NVIDIA GPU compute and SIMD optimizations during build
REM =============================================================================

cd /d c:\Users\Dean\source\Aspire-Full

REM Require NVIDIA GPU + CUDA runtime
where nvidia-smi >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] NVIDIA utilities (nvidia-smi) not found. Tensor builds require a CUDA-capable GPU.
    exit /b 1
)

echo [GPU] NVIDIA GPU detected - enabling compute acceleration
set CUDA_VISIBLE_DEVICES=all
set NVIDIA_VISIBLE_DEVICES=all
set NVIDIA_DRIVER_CAPABILITIES=compute,utility
set NVIDIA_REQUIRE_CUDA=cuda>=12.4,driver>=535
set TF_FORCE_GPU_ALLOW_GROWTH=true

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
