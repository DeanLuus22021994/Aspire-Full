# GitHub CLI Tooling

## Overview

This project includes comprehensive GitHub CLI tooling for AI-assisted development, automation, and repository management.

## Installed Extensions

| Extension | Command | Description |
|-----------|---------|-------------|
| gh-copilot | `gh copilot` | AI command suggestions and explanations |
| gh-models | `gh models` | Access GitHub Models API |
| gh-act | `gh act` | Run GitHub Actions locally |
| gh-dash | `gh dash` | Rich terminal dashboard |
| gh-aw | `gh aw` | Agentic Workflows (AI-powered) |
| gh-actions-cache | `gh actions-cache` | Manage Actions cache |
| gh-actions-importer | `gh actions-importer` | Migrate CI/CD pipelines |
| gh-sbom | `gh sbom` | Generate SBOMs |
| gh-projects | `gh projects` | Manage GitHub Projects |
| gh-poi | `gh poi` | Clean up local branches |
| gh-branch | `gh branch` | Fuzzy branch finder |
| gh-changelog | `gh changelog` | Generate release notes |
| gh-notify | `gh notify` | Terminal notifications |
| gh-s | `gh s` | Interactive search |

## Helper Scripts

Located in `scripts/`:

### Setup Extensions

```powershell
.\scripts\setup-gh-extensions.ps1
```

### Run Actions Locally

```powershell
# List available workflows
.\scripts\run-actions-locally.ps1 -List

# Run specific workflow
.\scripts\run-actions-locally.ps1 -Workflow build.yml

# Dry run
.\scripts\run-actions-locally.ps1 -DryRun
```

### GitHub Dashboard

```powershell
.\scripts\gh-dashboard.ps1
```

### Copilot Helper

```powershell
# Suggest a command
.\scripts\copilot-helper.ps1 suggest "list docker containers"

# Explain a command
.\scripts\copilot-helper.ps1 explain "docker ps -a"

# Setup aliases
.\scripts\copilot-helper.ps1 alias
```

### Models Helper

```powershell
# List available models
.\scripts\models-helper.ps1 list

# Start chat
.\scripts\models-helper.ps1 chat
```

### Agentic Workflows

```powershell
# Initialize
.\scripts\agentic-workflows.ps1 init

# Create workflow
.\scripts\agentic-workflows.ps1 new my-workflow

# Run workflow
.\scripts\agentic-workflows.ps1 run my-workflow
```

### Generate SBOM

```powershell
.\scripts\generate-sbom.ps1 -Output sbom.json
```

### Manage Actions Cache

```powershell
# List caches
.\scripts\actions-cache.ps1 list

# Clear all caches
.\scripts\actions-cache.ps1 clear
```

### Cleanup Branches

```powershell
# Dry run
.\scripts\cleanup-branches.ps1 -DryRun

# Actually clean
.\scripts\cleanup-branches.ps1
```

### Generate Changelog

```powershell
.\scripts\generate-changelog.ps1 -Version "1.0.0"
```

## Common Workflows

### AI-Assisted Development

```bash
# Get command suggestions
gh copilot suggest "create a new branch for feature X"

# Explain complex commands
gh copilot explain "git rebase -i HEAD~3"

# Chat with GitHub Models
gh models run
```

### Local CI/CD Testing

```bash
# Run all workflows locally
gh act push

# Run specific job
gh act -j build

# With secrets
gh act -s GITHUB_TOKEN=$GITHUB_TOKEN
```

### Repository Management

```bash
# Open dashboard
gh dash

# View notifications
gh notify

# Search repos interactively
gh s
```

### Release Management

```bash
# Generate changelog
gh changelog new

# Create release
gh release create v1.0.0 --generate-notes
```

## Best Practices

1. **Use Copilot for unfamiliar commands** - Get suggestions and explanations
2. **Test Actions locally** - Use `gh act` before pushing
3. **Keep branches clean** - Regularly run `gh poi`
4. **Generate SBOMs** - For security compliance
5. **Leverage Agentic Workflows** - For complex automation
