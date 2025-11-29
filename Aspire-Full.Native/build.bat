@echo off
echo Building AspireFullNative...

if not exist build mkdir build
cd build

cmake .. -DCMAKE_BUILD_TYPE=Release
if %errorlevel% neq 0 (
    echo CMake configuration failed. Ensure CMake and CUDA Toolkit are installed.
    exit /b %errorlevel%
)

cmake --build . --config Release
if %errorlevel% neq 0 (
    echo Build failed.
    exit /b %errorlevel%
)

echo Copying DLLs...
copy /Y Release\AspireFullNative.dll ..\..\Aspire-Full.Api\bin\Debug\net10.0\
copy /Y Release\AspireFullNative.dll ..\..\Aspire-Full.Tensor\bin\Debug\net10.0\

echo Native Build Complete.
cd ..
