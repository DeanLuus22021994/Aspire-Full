#!/bin/bash
set -e

echo "🚀 Running post-start setup..."

# Ensure PATH includes dotnet tools
export PATH="$PATH:/home/vscode/.dotnet/tools:/opt/aspire/bin"

# Verify Aspire Dashboard is accessible
echo "🔍 Checking Aspire Dashboard connectivity..."
for i in {1..10}; do
    if curl -sf http://aspire-dashboard:18888/health > /dev/null 2>&1; then
        echo "✅ Aspire Dashboard is ready at http://localhost:18888"
        break
    fi
    echo "⏳ Waiting for Aspire Dashboard... (attempt $i/10)"
    sleep 2
done

# Display environment info
echo ""
echo "📋 Environment Info:"
echo "   .NET SDK: $(dotnet --version)"
echo "   Aspire CLI: $(aspire --version 2>/dev/null || echo 'not in PATH')"
echo "   Docker: $(docker --version 2>/dev/null || echo 'not available')"
echo "   gh CLI: $(gh --version 2>/dev/null | head -1 || echo 'not available')"
echo ""
echo "🌐 Services:"
echo "   Aspire Dashboard: http://localhost:18888"
echo "   OTLP Endpoint: http://aspire-dashboard:18889"
echo ""
echo "✅ Development environment ready!"
