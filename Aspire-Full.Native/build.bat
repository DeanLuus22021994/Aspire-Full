@echo off
echo Building AspireFullNative via Docker...

cd ..
docker buildx bake -f Aspire-Full.DockerRegistry/docker-bake.hcl native-lib
if %errorlevel% neq 0 (
    echo Docker build failed.
    exit /b %errorlevel%
)

echo Native Build Complete.
