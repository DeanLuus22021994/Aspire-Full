variable "REGISTRY" {
  default = "host.docker.internal:5001"
}

variable "NAMESPACE" {
  default = "aspire"
}

variable "VERSION" {
  default = "1.0.0"
}

variable "ENVIRONMENT" {
  default = "dev"
}

variable "ARCH" {
  default = "linux-x64"
}

variable "TARGET_PLATFORMS" {
  # Multi-arch support: linux/amd64,linux/arm64
  default = "linux/amd64"
}

# =============================================================================
# Build Groups
# =============================================================================

group "default" {
  targets = ["api", "gateway", "python-agents", "tensor-compute"]
}

group "bootstrap" {
  targets = ["cuda-bootstrap-devel", "cuda-bootstrap-runtime", "base-native", "base-dotnet", "base-python"]
}

group "cuda-bootstrap" {
  targets = ["cuda-bootstrap-devel", "cuda-bootstrap-runtime"]
}

group "native-libs" {
  targets = ["native-lib-linux-x64", "native-lib-linux-arm64"]
}

group "runtime-minimal" {
  targets = ["api-minimal", "gateway-minimal"]
}

# =============================================================================
# CUDA Bootstrap Targets - Root of all TensorCore images
# Privileged GPU access for CUDA compilation
# =============================================================================

target "cuda-bootstrap-devel" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Nvidia/Dockerfile.cuda-bootstrap"
  target = "cuda-bootstrap-devel"
  tags = ["${REGISTRY}/${NAMESPACE}/cuda-bootstrap-devel:latest"]
  platforms = [TARGET_PLATFORMS]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cuda-bootstrap-devel-cache:latest",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cuda-bootstrap-devel-cache:latest,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
    BUILDKIT_CONTEXT_KEEP_GIT_DIR = "1"
  }
  # Privileged for GPU during build
  ssh = ["default"]
  secret = ["id=cuda_cache,src=/var/cache/cuda"]
}

target "cuda-bootstrap-runtime" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Nvidia/Dockerfile.cuda-bootstrap"
  target = "cuda-bootstrap-runtime"
  tags = ["${REGISTRY}/${NAMESPACE}/cuda-bootstrap-runtime:latest"]
  platforms = [TARGET_PLATFORMS]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cuda-bootstrap-runtime-cache:latest",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cuda-bootstrap-runtime-cache:latest,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

# =============================================================================
# Base Image Targets - Inherit from CUDA Bootstrap
# Enhanced caching for low-latency rebuilds
# =============================================================================

target "base-native" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Dockerfile.base-native"
  contexts = {
    "cuda-bootstrap-devel" = "target:cuda-bootstrap-devel"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/base-native:latest"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/base-native-cache:latest",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/base-native-cache:latest,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

target "base-dotnet" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Dockerfile.base-dotnet"
  contexts = {
    "cuda-bootstrap-runtime" = "target:cuda-bootstrap-runtime"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/base-dotnet:latest"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/base-dotnet-cache:latest",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/base-dotnet-cache:latest,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

target "base-python" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Dockerfile.base-python"
  contexts = {
    "cuda-bootstrap-devel" = "target:cuda-bootstrap-devel"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/base-python:latest"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/base-python-cache:latest",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/base-python-cache:latest,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

target "native-lib" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Native"
  contexts = {
    "base-native" = "target:base-native"
  }
  output = ["type=local,dest=Infra/Aspire-Full.Tensor.Core/build/"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/native-lib-cache:latest",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/native-lib-cache:latest,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
}

# Architecture-specific native library builds for NuGet package
target "native-lib-linux-x64" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Native"
  contexts = {
    "base-native" = "target:base-native"
  }
  platforms = ["linux/amd64"]
  output = ["type=local,dest=Infra/Aspire-Full.Tensor.Core/runtimes/linux-x64/native/"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/native-lib-cache:linux-x64",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/native-lib-cache:linux-x64,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
}

target "native-lib-linux-arm64" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Native"
  contexts = {
    "base-native" = "target:base-native"
  }
  platforms = ["linux/arm64"]
  output = ["type=local,dest=Infra/Aspire-Full.Tensor.Core/runtimes/linux-arm64/native/"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/native-lib-cache:linux-arm64",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/native-lib-cache:linux-arm64,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
}

# =============================================================================
# Application Targets - Standard builds
# =============================================================================

target "api" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Api"
  contexts = {
    "base-native" = "target:base-native"
    "base-dotnet" = "target:base-dotnet"
    "native-lib" = "target:native-lib"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/api-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/api-cache:${ENVIRONMENT}",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/api-cache:${ENVIRONMENT},mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

# =============================================================================
# Minimal Runtime Targets - Ultra-low RAM footprint for production
# =============================================================================

target "api-minimal" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Api"
  target = "runtime-minimal"
  contexts = {
    "base-dotnet" = "target:base-dotnet"
  }
  tags = [
    "${REGISTRY}/${NAMESPACE}/api-minimal-${ENVIRONMENT}:${VERSION}-${ARCH}",
    "${REGISTRY}/${NAMESPACE}/api-minimal:latest"
  ]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/api-minimal-cache:${ENVIRONMENT}",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/api-minimal-cache:${ENVIRONMENT},mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
    # Minimal GC settings for low RAM
    DOTNET_GCHeapHardLimit = "268435456"
    DOTNET_gcServer = "0"
    DOTNET_GCConserveMemory = "9"
  }
}

target "gateway-minimal" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Gateway"
  target = "runtime-minimal"
  contexts = {
    "base-dotnet" = "target:base-dotnet"
  }
  tags = [
    "${REGISTRY}/${NAMESPACE}/gateway-minimal-${ENVIRONMENT}:${VERSION}-${ARCH}",
    "${REGISTRY}/${NAMESPACE}/gateway-minimal:latest"
  ]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/gateway-minimal-cache:${ENVIRONMENT}",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/gateway-minimal-cache:${ENVIRONMENT},mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
    DOTNET_GCHeapHardLimit = "134217728"
    DOTNET_gcServer = "0"
    DOTNET_GCConserveMemory = "9"
  }
}

target "gateway" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Gateway"
  contexts = {
    "base-native" = "target:base-native"
    "base-dotnet" = "target:base-dotnet"
    "native-lib" = "target:native-lib"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/gateway-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/gateway-cache:${ENVIRONMENT}",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/gateway-cache:${ENVIRONMENT},mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

target "web" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Web"
  tags = ["${REGISTRY}/${NAMESPACE}/web-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/web-cache:${ENVIRONMENT}",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/web-cache:${ENVIRONMENT},mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

target "web-assembly" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.WebAssembly"
  contexts = {
    "base-dotnet" = "target:base-dotnet"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/web-assembly-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/web-assembly-cache:${ENVIRONMENT}",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/web-assembly-cache:${ENVIRONMENT},mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

target "python-agents" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.PythonAgent"
  contexts = {
    "base-python" = "target:base-python"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/python-agents-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/python-agents-cache:${ENVIRONMENT}",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/python-agents-cache:${ENVIRONMENT},mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

target "tensor-compute" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Tensor"
  contexts = {
    "cuda-bootstrap-devel" = "target:cuda-bootstrap-devel"
    "cuda-bootstrap-runtime" = "target:cuda-bootstrap-runtime"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/tensor-compute-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/tensor-compute-cache:${ENVIRONMENT}",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/tensor-compute-cache:${ENVIRONMENT},mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}
