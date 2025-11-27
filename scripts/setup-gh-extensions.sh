#!/bin/bash
# GitHub CLI Extensions Setup Script (Linux/macOS)
# Run this to install all recommended gh extensions

set -e

extensions=(
    "github/gh-copilot"
    "github/gh-models"
    "github/gh-actions-importer"
    "actions/gh-actions-cache"
    "nektos/gh-act"
    "dlvhdr/gh-dash"
    "advanced-security/gh-sbom"
    "github/gh-projects"
    "seachicken/gh-poi"
    "gennaro-tedesco/gh-s"
    "mislav/gh-branch"
    "chelnak/gh-changelog"
    "meiji163/gh-notify"
    "githubnext/gh-aw"
)

echo "🚀 Installing GitHub CLI Extensions..."
echo ""

for ext in "${extensions[@]}"; do
    echo "📦 Installing $ext..."
    if gh extension install "$ext" 2>/dev/null; then
        echo "   ✅ Installed"
    else
        echo "   ⏭️ Already installed or skipped"
    fi
done

echo ""
echo "✅ All extensions installed!"
echo ""
echo "📋 Installed extensions:"
gh extension list
