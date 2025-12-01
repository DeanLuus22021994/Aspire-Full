using Aspire_Full.Pipeline.Modules.Discovery;
using Aspire_Full.Pipeline.Utils;

namespace Aspire_Full.Pipeline.Modules.Discovery.Components;

public class RepoComponent : IDiscoveryComponent
{
    public async Task<DiscoveryResult> DiscoverAsync(EnvironmentConfig config)
    {
        string root;
        try
        {
            root = LocateRepositoryRoot();
        }
        catch
        {
            return new DiscoveryResult("Repository", "Missing", "Could not locate .slnf or .slnx", new());
        }

        var details = new Dictionary<string, string>
        {
            ["Path"] = root
        };

        // Git Info
        try
        {
            var branch = await ProcessUtils.RunAsync("git", ["rev-parse", "--abbrev-ref", "HEAD"], root);
            if (branch.ExitCode == 0) details["Branch"] = branch.Output.Trim();

            var commit = await ProcessUtils.RunAsync("git", ["rev-parse", "--short", "HEAD"], root);
            if (commit.ExitCode == 0) details["Commit"] = commit.Output.Trim();
        }
        catch { /* Git might not be in path */ }

        config.Repository.Root = root.Replace("\\", "/");
        config.Repository.Branch = details.GetValueOrDefault("Branch", "main");

        return new DiscoveryResult("Repository", "Found", root, details);
    }

    public static string LocateRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        if (ContainsSolutionMarker(current)) return current;

        var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        if (ContainsSolutionMarker(candidate)) return candidate;

        // Try walking up
        var walk = current;
        while (!string.IsNullOrEmpty(walk))
        {
            if (ContainsSolutionMarker(walk)) return walk;
            walk = Path.GetDirectoryName(walk);
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");

        static bool ContainsSolutionMarker(string path)
        {
            return File.Exists(Path.Combine(path, "Aspire-Full.slnf")) ||
                   File.Exists(Path.Combine(path, "Aspire-Full.slnx"));
        }
    }
}
