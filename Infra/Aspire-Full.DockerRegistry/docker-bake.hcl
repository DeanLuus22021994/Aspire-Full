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

# =============================================================================
# CUDA Bootstrap Targets - Root of all TensorCore images
# =============================================================================

target "cuda-bootstrap-devel" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Nvidia/Dockerfile.cuda-bootstrap"
  target = "cuda-bootstrap-devel"
  tags = ["${REGISTRY}/${NAMESPACE}/cuda-bootstrap-devel:latest"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/cuda-bootstrap-devel-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/cuda-bootstrap-devel-cache:latest,mode=max"]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

target "cuda-bootstrap-runtime" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Nvidia/Dockerfile.cuda-bootstrap"
  target = "cuda-bootstrap-runtime"
  tags = ["${REGISTRY}/${NAMESPACE}/cuda-bootstrap-runtime:latest"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/cuda-bootstrap-runtime-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/cuda-bootstrap-runtime-cache:latest,mode=max"]
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}

# =============================================================================
# Base Image Targets - Inherit from CUDA Bootstrap
# =============================================================================

target "base-native" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Dockerfile.base-native"
  contexts = {
    "cuda-bootstrap-devel" = "target:cuda-bootstrap-devel"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/base-native:latest"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-native-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-native-cache:latest,mode=max"]
}

target "base-dotnet" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Dockerfile.base-dotnet"
  contexts = {
    "cuda-bootstrap-runtime" = "target:cuda-bootstrap-runtime"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/base-dotnet:latest"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-dotnet-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-dotnet-cache:latest,mode=max"]
}

target "base-python" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Dockerfile.base-python"
  contexts = {
    "cuda-bootstrap-devel" = "target:cuda-bootstrap-devel"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/base-python:latest"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-python-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-python-cache:latest,mode=max"]
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
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/native-lib-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/native-lib-cache:latest,mode=max"]
}

target "api" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Api"
  contexts = {
    "base-native" = "target:base-native"
    "base-dotnet" = "target:base-dotnet"
    "native-lib" = "target:native-lib"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/api-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/api-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/api-cache:${ENVIRONMENT},mode=max"]
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
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/gateway-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/gateway-cache:${ENVIRONMENT},mode=max"]
}

target "web" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Web"
  tags = ["${REGISTRY}/${NAMESPACE}/web-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/web-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/web-cache:${ENVIRONMENT},mode=max"]
}

target "web-assembly" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.WebAssembly"
  contexts = {
    "base-dotnet" = "target:base-dotnet"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/web-assembly-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/web-assembly-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/web-assembly-cache:${ENVIRONMENT},mode=max"]
}

target "python-agents" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.PythonAgent"
  tags = ["${REGISTRY}/${NAMESPACE}/python-agents-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/python-agents-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/python-agents-cache:${ENVIRONMENT},mode=max"]
  # TensorCore acceleration - all dependencies baked with CUDA support
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
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/tensor-compute-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/tensor-compute-cache:${ENVIRONMENT},mode=max"]
  # TensorCore acceleration - Python 3.15t free-threaded with CUDA
  args = {
    BUILDKIT_INLINE_CACHE = "1"
  }
}
