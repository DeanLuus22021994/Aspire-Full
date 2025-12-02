# =============================================================================
# ASPIRE-FULL UNIFIED DOCKER BAKE - K8s NVIDIA Device Plugin Compatible
# Ultra-low latency builds with factory pattern
# =============================================================================
# Usage:
#   docker buildx bake                    # Build default targets
#   docker buildx bake api gateway        # Build specific targets
#   docker buildx bake --push             # Build and push to registry
#   docker buildx bake bootstrap          # Build base images only
# =============================================================================

variable "REGISTRY" {
  default = "localhost:5001"
}

variable "NAMESPACE" {
  default = "aspire"
}

variable "VERSION" {
  default = "1.0.0"
}

variable "CUDA_VERSION" {
  default = "13.0.0"
}

variable "DOTNET_VERSION" {
  default = "10.0"
}

variable "PYTHON_VERSION" {
  default = "3.15t"
}

# =============================================================================
# Build Groups
# =============================================================================

group "default" {
  targets = ["api", "gateway", "tensor"]
}

group "all" {
  targets = ["api", "gateway", "tensor", "web", "native"]
}

group "minimal" {
  targets = ["api", "gateway"]
}

group "bootstrap" {
  targets = ["native"]
}

# =============================================================================
# Shared Target Configuration - Inherited by all targets
# =============================================================================

target "_common" {
  context = "../../../.."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Dockerfile.unified"
  args = {
    CUDA_VERSION = CUDA_VERSION
    DOTNET_VERSION = DOTNET_VERSION
    PYTHON_VERSION = PYTHON_VERSION
    BUILDKIT_INLINE_CACHE = "1"
  }
  cache-from = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cache:buildkit",
    "type=local,src=/tmp/.buildx-cache"
  ]
  cache-to = [
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  # K8s NVIDIA Device Plugin annotations
  annotations = [
    "nvidia.com/gpu.deploy.device-plugin=true",
    "nvidia.com/gpu.deploy.container-toolkit=true"
  ]
}

# =============================================================================
# Production Targets - Ultra-minimal runtime RAM
# =============================================================================

target "api" {
  inherits = ["_common"]
  target = "api"
  tags = [
    "${REGISTRY}/${NAMESPACE}/api:${VERSION}",
    "${REGISTRY}/${NAMESPACE}/api:latest"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cache:api,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  platforms = ["linux/amd64"]
}

target "gateway" {
  inherits = ["_common"]
  target = "gateway"
  tags = [
    "${REGISTRY}/${NAMESPACE}/gateway:${VERSION}",
    "${REGISTRY}/${NAMESPACE}/gateway:latest"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cache:gateway,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  platforms = ["linux/amd64"]
}

target "tensor" {
  inherits = ["_common"]
  target = "tensor"
  tags = [
    "${REGISTRY}/${NAMESPACE}/tensor:${VERSION}",
    "${REGISTRY}/${NAMESPACE}/tensor:latest"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cache:tensor,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  platforms = ["linux/amd64"]
}

target "web" {
  inherits = ["_common"]
  target = "web"
  tags = [
    "${REGISTRY}/${NAMESPACE}/web:${VERSION}",
    "${REGISTRY}/${NAMESPACE}/web:latest"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cache:web,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  platforms = ["linux/amd64"]
}

target "native" {
  inherits = ["_common"]
  target = "native"
  output = ["type=local,dest=../../../../AI/Aspire-Full.Tensor/Native/build/"]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cache:native,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
}

target "devcontainer" {
  inherits = ["_common"]
  target = "devcontainer"
  tags = [
    "${REGISTRY}/${NAMESPACE}/devcontainer:${VERSION}",
    "${REGISTRY}/${NAMESPACE}/devcontainer:latest"
  ]
  cache-to = [
    "type=registry,ref=${REGISTRY}/${NAMESPACE}/cache:devcontainer,mode=max",
    "type=local,dest=/tmp/.buildx-cache-new,mode=max"
  ]
  platforms = ["linux/amd64"]
}

# =============================================================================
# Multi-arch targets (when needed)
# =============================================================================

target "api-multiarch" {
  inherits = ["api"]
  platforms = ["linux/amd64", "linux/arm64"]
}

target "gateway-multiarch" {
  inherits = ["gateway"]
  platforms = ["linux/amd64", "linux/arm64"]
}
