namespace Aspire_Full.Pipeline.Utils;

public static class GitUtils
{
    public static string GetRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        if (ContainsSolutionMarker(current))
            return current;

        var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        if (ContainsSolutionMarker(candidate))
            return candidate;

        // Try walking up
        var dir = new DirectoryInfo(current);
        while (dir != null)
        {
            if (ContainsSolutionMarker(dir.FullName))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root (Aspire-Full.slnf not found).");
    }

    private static bool ContainsSolutionMarker(string path)
    {
        return File.Exists(Path.Combine(path, "Aspire-Full.slnf")) ||
               File.Exists(Path.Combine(path, "Aspire-Full.slnx"));
    }
}
