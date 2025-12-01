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

group "default" {
  targets = ["api", "gateway", "python-agents", "tensor-compute"]
}

group "bootstrap" {
  targets = ["base-native", "base-dotnet", "base-python"]
}

target "base-native" {
  context = "Infra/Aspire-Full.DockerRegistry/docker"
  dockerfile = "Dockerfile.base-native"
  tags = ["${REGISTRY}/${NAMESPACE}/base-native:latest"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-native-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-native-cache:latest,mode=max"]
}

target "base-dotnet" {
  context = "Infra/Aspire-Full.DockerRegistry/docker"
  dockerfile = "Dockerfile.base-dotnet"
  tags = ["${REGISTRY}/${NAMESPACE}/base-dotnet:latest"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-dotnet-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-dotnet-cache:latest,mode=max"]
}

target "base-python" {
  context = "Infra/Aspire-Full.DockerRegistry/docker"
  dockerfile = "Dockerfile.base-python"
  tags = ["${REGISTRY}/${NAMESPACE}/base-python:latest"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-python-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-python-cache:latest,mode=max"]
}

target "native-lib" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Native"
  contexts = {
    "base-native" = "target:base-native"
  }
  output = ["type=local,dest=AI/Aspire-Full.Tensor/build/"]
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
  contexts = {
    "base-python" = "target:base-python"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/python-agents-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/python-agents-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/python-agents-cache:${ENVIRONMENT},mode=max"]
}

target "tensor-compute" {
  context = "."
  dockerfile = "Infra/Aspire-Full.DockerRegistry/docker/Aspire/Dockerfile.Tensor"
  tags = ["${REGISTRY}/${NAMESPACE}/tensor-compute-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/tensor-compute-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/tensor-compute-cache:${ENVIRONMENT},mode=max"]
}
