using System.Text;
using Spectre.Console;
using Aspire_Full.Pipeline.Utils;
using Aspire_Full.Pipeline.Constants;

namespace Aspire_Full.Pipeline.Services;

public class DocsService
{
    public async Task GenerateDocsAsync(bool api, bool llms, bool all)
    {
        var root = GitUtils.GetRepositoryRoot();

        if (api || all)
        {
            AnsiConsole.MarkupLine("[yellow]Generating API documentation...[/]");

            // Build first
            AnsiConsole.MarkupLine("Building project...");
            await ProcessUtils.RunAsync("dotnet", ["build", PathConstants.SolutionFilter, "--configuration", "Release"], root, silent: false);

            var dllPath = Path.Combine(root, "Aspire-Full", "bin", "Release", "net10.0", "Aspire-Full.dll");
            var outputPath = Path.Combine(root, "docs", "api");

            if (File.Exists(dllPath))
            {
                // Check xmldoc2md
                await ProcessUtils.RunAsync("dotnet", ["tool", "install", "-g", "XMLDoc2Markdown"], silent: true);

                AnsiConsole.MarkupLine($"Generating docs to {outputPath}...");
                await ProcessUtils.RunAsync("xmldoc2md", [dllPath, "--output", outputPath, "--github-pages", "--back-button", "--structure", "tree"], root, silent: false);
                AnsiConsole.MarkupLine("[green]API docs generated.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]DLL not found. Build failed?[/]");
            }
        }

        if (llms || all)
        {
            AnsiConsole.MarkupLine("[yellow]Updating llms.txt...[/]");
            await UpdateLlmsTxtAsync(root);
            AnsiConsole.MarkupLine("[green]llms.txt updated.[/]");
        }
    }

    private async Task UpdateLlmsTxtAsync(string root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Aspire-Full");
        sb.AppendLine();
        sb.AppendLine("> .NET Aspire Full AppHost with bleeding-edge tooling for AI-assisted development");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("This project demonstrates a complete .NET Aspire distributed application setup with:");
        sb.AppendLine("- .NET 10 SDK (LTS)");
        sb.AppendLine("- Aspire 13.1.0-preview (bleeding edge)");
        sb.AppendLine("- Docker-based devcontainer with named volumes");
        sb.AppendLine("- GitHub CLI tooling with AI extensions");
        sb.AppendLine("- AI-optimized documentation");
        sb.AppendLine();
        sb.AppendLine("## Documentation");
        sb.AppendLine();

        var docsPath = Path.Combine(root, "docs");
        if (Directory.Exists(docsPath))
        {
            var mdFiles = Directory.GetFiles(docsPath, "*.md", SearchOption.AllDirectories);
            foreach (var file in mdFiles)
            {
                var relativePath = Path.GetRelativePath(root, file).Replace("\\", "/");
                var lines = await File.ReadAllLinesAsync(file);
                var title = lines.FirstOrDefault(l => l.StartsWith("# "))?.TrimStart('#', ' ').Trim() ?? Path.GetFileNameWithoutExtension(file);
                var content = string.Join(Environment.NewLine, lines);
                var tokens = content.Length / 4; // Rough estimate
                var lastModified = File.GetLastWriteTime(file).ToString("yyyy-MM-dd");

                sb.AppendLine($"- [{title}](https://github.com/DeanLuus22021994/Aspire-Full/blob/master/{relativePath}): ~{tokens} tokens, updated {lastModified}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Quick Start");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Clone the repository");
        sb.AppendLine("git clone https://github.com/DeanLuus22021994/Aspire-Full.git");
        sb.AppendLine("cd Aspire-Full");
        sb.AppendLine();
        sb.AppendLine("# Run with Aspire CLI");
        sb.AppendLine("aspire run");
        sb.AppendLine();
        sb.AppendLine("# Or with dotnet");
        sb.AppendLine("dotnet run --project Aspire-Full");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Key Technologies");
        sb.AppendLine();
        sb.AppendLine("| Technology | Version | Purpose |");
        sb.AppendLine("|------------|---------|---------|");
        sb.AppendLine("| .NET SDK | 10.0.100 | Runtime and build |");
        sb.AppendLine("| Aspire | 13.1.0-preview | Distributed app orchestration |");
        sb.AppendLine("| Docker | 28.x | Containerization |");
        sb.AppendLine("| GitHub CLI | 2.83+ | Repository management |");
        sb.AppendLine();
        sb.AppendLine("## Project Structure");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine("Aspire-Full/");
        sb.AppendLine("├── .devcontainer/          # Docker devcontainer config");
        sb.AppendLine("├── .github/workflows/      # CI/CD pipelines");
        sb.AppendLine("├── Aspire-Full/            # AppHost project");
        sb.AppendLine("├── docs/                   # Documentation");
        sb.AppendLine("├── scripts/                # Automation scripts");
        sb.AppendLine("└── llms.txt                # AI documentation index (this file)");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Optional");
        sb.AppendLine();
        sb.AppendLine("- [Contributing Guidelines](https://github.com/DeanLuus22021994/Aspire-Full/blob/master/CONTRIBUTING.md)");
        sb.AppendLine("- [Changelog](https://github.com/DeanLuus22021994/Aspire-Full/blob/master/CHANGELOG.md)");

        await File.WriteAllTextAsync(Path.Combine(root, "llms.txt"), sb.ToString());
    }

    public async Task GenerateChangelogAsync(string version)
    {
        await GhUtils.EnsureExtensionAsync("chelnak/gh-changelog");
        var root = GitUtils.GetRepositoryRoot();

        var args = new List<string> { "changelog", "new" };
        if (!string.IsNullOrEmpty(version))
        {
            args.Add("--next-version");
            args.Add(version);
        }

        AnsiConsole.MarkupLine("[cyan]Generating Changelog...[/]");
        await ProcessUtils.RunAsync("gh", args.ToArray(), root, silent: false);
    }
}
