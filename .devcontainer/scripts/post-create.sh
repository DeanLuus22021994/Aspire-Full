#!/bin/bash
set -e

echo "🚀 Running post-create setup..."

# Ensure PATH includes dotnet tools
export PATH="$PATH:/home/vscode/.dotnet/tools:/opt/aspire/bin"

# Clone repo content to workspace if empty
if [ ! -f "/workspace/Aspire-Full.slnf" ]; then
    echo "📥 Cloning repository to workspace volume..."
    cd /workspace
    git clone https://github.com/DeanLuus22021994/Aspire-Full.git . 2>/dev/null || true
fi

# Execute canonical pipeline (clean/restore/format/build)
if [ -f "/workspace/Aspire-Full.slnf" ]; then
    echo "📦 Running PipelineRunner (skip run)..."
    cd /workspace
    dotnet run --project tools/PipelineRunner/PipelineRunner.csproj -- --skip-run || true
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

echo ""
echo "📋 Self-hosted runner info:"
echo "   The GitHub Actions runner runs as a separate service."
echo "   To configure it, set GITHUB_TOKEN environment variable."
echo "   Use: scripts/manage-runner.ps1 -Action setup -Token <token>"
echo ""

echo "✅ Post-create setup complete!"
