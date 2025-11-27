# Documentation Generator
# Generates API documentation from XML comments and creates AI-friendly llms.txt

param(
    [switch]$Build,
    [switch]$GenerateApi,
    [switch]$UpdateLlms,
    [switch]$All
)

$ErrorActionPreference = "Stop"

Write-Host "📚 Documentation Generator" -ForegroundColor Cyan
Write-Host ""

Push-Location $PSScriptRoot\..

try {
    # Build if requested or if doing all
    if ($Build -or $All) {
        Write-Host "🔨 Building project with XML documentation..." -ForegroundColor Yellow
        dotnet build Aspire-Full.slnx --configuration Release
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }
        Write-Host "   ✅ Build complete" -ForegroundColor Green
    }

    # Generate API documentation
    if ($GenerateApi -or $All) {
        Write-Host "📄 Generating API documentation..." -ForegroundColor Yellow

        $dllPath = "Aspire-Full\bin\Release\net10.0\Aspire-Full.dll"
        $outputPath = "docs\api"

        if (Test-Path $dllPath) {
            # Check if xmldoc2md is installed
            $xmldoc2md = Get-Command xmldoc2md -ErrorAction SilentlyContinue
            if (-not $xmldoc2md) {
                Write-Host "   📦 Installing XMLDoc2Markdown..." -ForegroundColor Yellow
                dotnet tool install -g XMLDoc2Markdown
            }

            xmldoc2md $dllPath --output $outputPath --github-pages --back-button --structure tree
            Write-Host "   ✅ API docs generated at $outputPath" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️ DLL not found. Run with -Build first." -ForegroundColor Yellow
        }
    }

    # Update llms.txt with current documentation
    if ($UpdateLlms -or $All) {
        Write-Host "🤖 Updating llms.txt for AI discoverability..." -ForegroundColor Yellow

        $llmsContent = @"
# Aspire-Full

> .NET Aspire Full AppHost with bleeding-edge tooling for AI-assisted development

## Overview

This project demonstrates a complete .NET Aspire distributed application setup with:
- .NET 10 SDK (LTS)
- Aspire 13.1.0-preview (bleeding edge)
- Docker-based devcontainer with named volumes
- GitHub CLI tooling with AI extensions
- AI-optimized documentation

## Documentation

"@

        # Scan for markdown files in docs
        $docsPath = "docs"
        if (Test-Path $docsPath) {
            $mdFiles = Get-ChildItem -Path $docsPath -Filter "*.md" -Recurse
            foreach ($file in $mdFiles) {
                $relativePath = $file.FullName.Replace("$PWD\", "").Replace("\", "/")
                $title = (Get-Content $file.FullName -First 1) -replace "^#\s*", ""
                $tokens = [math]::Round((Get-Content $file.FullName -Raw).Length / 4)
                $lastModified = $file.LastWriteTime.ToString("yyyy-MM-dd")

                $llmsContent += "- [$title](https://github.com/DeanLuus22021994/Aspire-Full/blob/master/$relativePath): ~$tokens tokens, updated $lastModified`n"
            }
        }

        $llmsContent += @"

## Quick Start

```bash
# Clone the repository
git clone https://github.com/DeanLuus22021994/Aspire-Full.git
cd Aspire-Full

# Run with Aspire CLI
aspire run

# Or with dotnet
dotnet run --project Aspire-Full
```

## Key Technologies

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET SDK | 10.0.100 | Runtime and build |
| Aspire | 13.1.0-preview | Distributed app orchestration |
| Docker | 28.x | Containerization |
| GitHub CLI | 2.83+ | Repository management |

## Project Structure

```
Aspire-Full/
├── .devcontainer/          # Docker devcontainer config
├── .github/workflows/      # CI/CD pipelines
├── Aspire-Full/            # AppHost project
├── docs/                   # Documentation
├── scripts/                # Automation scripts
└── llms.txt                # AI documentation index (this file)
```

## Optional

- [Contributing Guidelines](https://github.com/DeanLuus22021994/Aspire-Full/blob/master/CONTRIBUTING.md)
- [Changelog](https://github.com/DeanLuus22021994/Aspire-Full/blob/master/CHANGELOG.md)
"@

        $llmsContent | Out-File -FilePath "llms.txt" -Encoding utf8
        Write-Host "   ✅ llms.txt updated" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "✅ Documentation generation complete!" -ForegroundColor Green

} finally {
    Pop-Location
}
