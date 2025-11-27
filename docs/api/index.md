# Aspire-Full API Reference

## Overview

Auto-generated API documentation from XML comments using [XMLDoc2Markdown](https://github.com/charlesdevandiere/xmldoc2md).

## Generation

To regenerate API documentation:

```powershell
.\scripts\generate-docs.ps1 -All
```

Or manually:

```bash
dotnet build Aspire-Full.slnx --configuration Release
xmldoc2md Aspire-Full\bin\Release\net10.0\Aspire-Full.dll --output docs/api --github-pages
```

## Namespaces

- **Aspire-Full** - Main application namespace

## AI Context

This documentation is optimized for AI assistants through:

1. **llms.txt** - Standard AI documentation index at repository root
2. **Markdown format** - Minimal noise, maximum context
3. **Structured comments** - XML documentation with examples
4. **Token efficiency** - ~95% reduction vs HTML docs
