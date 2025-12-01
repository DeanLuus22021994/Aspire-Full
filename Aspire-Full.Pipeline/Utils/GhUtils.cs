using Spectre.Console;

namespace Aspire_Full.Pipeline.Utils;

public static class GhUtils
{
    public static async Task EnsureExtensionAsync(string extensionName)
    {
        var (code, output) = await ProcessUtils.RunAsync("gh", ["extension", "list"], silent: true);
        if (!output.Contains(extensionName))
        {
            AnsiConsole.MarkupLine($"[yellow]Installing gh extension: {extensionName}...[/]");
            await ProcessUtils.RunAsync("gh", ["extension", "install", extensionName], silent: false);
        }
    }

    public static async Task<string> GetRepoAsync()
    {
        var (code, output) = await ProcessUtils.RunAsync("gh", ["repo", "view", "--json", "nameWithOwner", "-q", ".nameWithOwner"], silent: true);
        return output.Trim();
    }
}
