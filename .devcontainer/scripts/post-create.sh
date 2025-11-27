#!/bin/bash
set -e

echo "🚀 Running post-create setup..."

# Ensure PATH includes dotnet tools
export PATH="$PATH:/home/vscode/.dotnet/tools:/opt/aspire/bin"

# Clone repo content to workspace if empty
if [ ! -f "/workspace/Aspire-Full.slnx" ]; then
    echo "📥 Cloning repository to workspace volume..."
    cd /workspace
    git clone https://github.com/DeanLuus22021994/Aspire-Full.git . 2>/dev/null || true
fi

# Restore NuGet packages
if [ -f "/workspace/Aspire-Full.slnx" ]; then
    echo "📦 Restoring NuGet packages..."
    cd /workspace
    dotnet restore Aspire-Full.slnx || true
fi

# Update global tools
echo "🔧 Updating global .NET tools..."
dotnet tool update -g dotnet-ef || true
dotnet tool update -g dotnet-outdated-tool || true

# Setup git safe directory
git config --global --add safe.directory /workspace

# Install GitHub CLI extensions
echo "📦 Installing GitHub CLI extensions..."
gh extension install github/gh-copilot 2>/dev/null || true
gh extension install github/gh-models 2>/dev/null || true
gh extension install nektos/gh-act 2>/dev/null || true
gh extension install dlvhdr/gh-dash 2>/dev/null || true
gh extension install advanced-security/gh-sbom 2>/dev/null || true
gh extension install github/gh-projects 2>/dev/null || true
gh extension install actions/gh-actions-cache 2>/dev/null || true
gh extension install githubnext/gh-aw 2>/dev/null || true
gh extension install seachicken/gh-poi 2>/dev/null || true
gh extension install chelnak/gh-changelog 2>/dev/null || true

echo "✅ Post-create setup complete!"
