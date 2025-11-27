#!/bin/bash
set -e

# GitHub Actions Runner Entrypoint Script
# Handles registration, startup, and graceful shutdown

RUNNER_NAME=${RUNNER_NAME:-"aspire-runner-$(hostname)"}
RUNNER_LABELS=${RUNNER_LABELS:-"self-hosted,Linux,X64,docker,dotnet,aspire"}
RUNNER_GROUP=${RUNNER_GROUP:-"Default"}
RUNNER_WORKDIR=${RUNNER_WORKDIR:-"/home/runner/_work"}

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Wait for Docker-in-Docker to be ready
wait_for_docker() {
    log_info "Waiting for Docker daemon..."
    local max_attempts=30
    local attempt=1

    while [ $attempt -le $max_attempts ]; do
        if docker info > /dev/null 2>&1; then
            log_info "Docker daemon is ready"
            return 0
        fi
        log_warn "Docker not ready, attempt $attempt/$max_attempts..."
        sleep 2
        ((attempt++))
    done

    log_error "Docker daemon did not become ready in time"
    return 1
}

# Get registration token from GitHub API
get_registration_token() {
    if [ -z "$GITHUB_TOKEN" ]; then
        log_error "GITHUB_TOKEN environment variable is required"
        exit 1
    fi

    if [ -z "$GITHUB_REPOSITORY" ]; then
        log_error "GITHUB_REPOSITORY environment variable is required (format: owner/repo)"
        exit 1
    fi

    log_info "Obtaining registration token for $GITHUB_REPOSITORY..."

    local response
    response=$(curl -s -X POST \
        -H "Authorization: token ${GITHUB_TOKEN}" \
        -H "Accept: application/vnd.github+json" \
        -H "X-GitHub-Api-Version: 2022-11-28" \
        "https://api.github.com/repos/${GITHUB_REPOSITORY}/actions/runners/registration-token")

    RUNNER_TOKEN=$(echo "$response" | jq -r '.token')

    if [ -z "$RUNNER_TOKEN" ] || [ "$RUNNER_TOKEN" = "null" ]; then
        log_error "Failed to get registration token. Response: $response"
        exit 1
    fi

    log_info "Registration token obtained successfully"
}

# Configure the runner
configure_runner() {
    # Check if already configured
    if [ -f ".runner" ]; then
        log_info "Runner already configured, skipping configuration"
        return 0
    fi

    get_registration_token

    log_info "Configuring runner: $RUNNER_NAME"
    log_info "Labels: $RUNNER_LABELS"
    log_info "Work directory: $RUNNER_WORKDIR"

    ./config.sh \
        --url "https://github.com/${GITHUB_REPOSITORY}" \
        --token "$RUNNER_TOKEN" \
        --name "$RUNNER_NAME" \
        --labels "$RUNNER_LABELS" \
        --runnergroup "$RUNNER_GROUP" \
        --work "$RUNNER_WORKDIR" \
        --unattended \
        --replace

    log_info "Runner configured successfully"
}

# Remove runner on shutdown
cleanup() {
    log_info "Received shutdown signal, removing runner..."

    if [ -f ".runner" ]; then
        # Try to get a removal token
        if [ -n "$GITHUB_TOKEN" ] && [ -n "$GITHUB_REPOSITORY" ]; then
            local response
            response=$(curl -s -X POST \
                -H "Authorization: token ${GITHUB_TOKEN}" \
                -H "Accept: application/vnd.github+json" \
                -H "X-GitHub-Api-Version: 2022-11-28" \
                "https://api.github.com/repos/${GITHUB_REPOSITORY}/actions/runners/remove-token")

            local remove_token
            remove_token=$(echo "$response" | jq -r '.token')

            if [ -n "$remove_token" ] && [ "$remove_token" != "null" ]; then
                ./config.sh remove --token "$remove_token" || true
                log_info "Runner removed from GitHub"
            fi
        fi
    fi

    exit 0
}

# Set up signal handlers for graceful shutdown
trap cleanup SIGTERM SIGINT SIGQUIT

# Main entrypoint
main() {
    log_info "=========================================="
    log_info "GitHub Actions Self-Hosted Runner"
    log_info "=========================================="
    log_info "Runner Version: $(cat /home/runner/actions-runner/bin/runner.version 2>/dev/null || echo 'unknown')"
    log_info ".NET SDK: $(dotnet --version 2>/dev/null || echo 'not available')"
    log_info "Node.js: $(node --version 2>/dev/null || echo 'not available')"
    log_info "npm: $(npm --version 2>/dev/null || echo 'not available')"
    log_info "=========================================="

    # Wait for Docker daemon
    wait_for_docker

    log_info "Docker: $(docker --version 2>/dev/null || echo 'not available')"

    # Configure runner if needed
    configure_runner

    log_info "Starting runner..."

    # Run the runner
    exec ./run.sh
}

main "$@"
