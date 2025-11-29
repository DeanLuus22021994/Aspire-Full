variable "REGISTRY" {
  default = "localhost:5001"
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
  targets = ["api", "gateway", "web", "web-assembly", "python-agents"]
}

group "bootstrap" {
  targets = ["base-native", "base-dotnet"]
}

target "base-native" {
  context = "docker"
  dockerfile = "Dockerfile.base-native"
  tags = ["${REGISTRY}/${NAMESPACE}/base-native:latest"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-native-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-native-cache:latest,mode=max"]
}

target "base-dotnet" {
  context = "docker"
  dockerfile = "Dockerfile.base-dotnet"
  tags = ["${REGISTRY}/${NAMESPACE}/base-dotnet:latest"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-dotnet-cache:latest"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/base-dotnet-cache:latest,mode=max"]
}

target "api" {
  context = "."
  dockerfile = "Aspire-Full.Api/Dockerfile"
  contexts = {
    "base-native" = "target:base-native"
    "base-dotnet" = "target:base-dotnet"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/api-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/api-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/api-cache:${ENVIRONMENT},mode=max"]
}

target "gateway" {
  context = "."
  dockerfile = "Aspire-Full.Gateway/Dockerfile"
  contexts = {
    "base-native" = "target:base-native"
    "base-dotnet" = "target:base-dotnet"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/gateway-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/gateway-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/gateway-cache:${ENVIRONMENT},mode=max"]
}

target "web" {
  context = "Aspire-Full.Web"
  dockerfile = "Dockerfile"
  tags = ["${REGISTRY}/${NAMESPACE}/web-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/web-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/web-cache:${ENVIRONMENT},mode=max"]
}

target "web-assembly" {
  context = "."
  dockerfile = "Aspire-Full.WebAssembly/Dockerfile"
  contexts = {
    "base-dotnet" = "target:base-dotnet"
  }
  tags = ["${REGISTRY}/${NAMESPACE}/web-assembly-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/web-assembly-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/web-assembly-cache:${ENVIRONMENT},mode=max"]
}

target "python-agents" {
  context = "Aspire-Full.Python/python-agents"
  dockerfile = "Dockerfile.agent"
  tags = ["${REGISTRY}/${NAMESPACE}/python-agents-${ENVIRONMENT}:${VERSION}-${ARCH}"]
  cache-from = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/python-agents-cache:${ENVIRONMENT}"]
  cache-to = ["type=registry,ref=${REGISTRY}/${NAMESPACE}/python-agents-cache:${ENVIRONMENT},mode=max"]
}
