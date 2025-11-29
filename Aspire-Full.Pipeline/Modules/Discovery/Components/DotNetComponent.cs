using Aspire_Full.Pipeline.Modules.Discovery;
using Aspire_Full.Pipeline.Utils;

namespace Aspire_Full.Pipeline.Modules.Discovery.Components;

public class DotNetComponent : IDiscoveryComponent
{
    public async Task<DiscoveryResult> DiscoverAsync()
    {
        var details = new Dictionary<string, string>();
        var version = await ProcessUtils.RunAsync("dotnet", ["--version"]);

        if (version.ExitCode != 0)
            return new DiscoveryResult(".NET SDK", "Missing", "dotnet command not found", details);

        details["Version"] = version.Output.Trim();

        var info = await ProcessUtils.RunAsync("dotnet", ["--info"]);
        if (info.ExitCode == 0)
        {
            var lines = info.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Find SDKs
            var sdkList = new List<string>();
            bool inSdks = false;
            foreach (var line in lines)
            {
                if (line.StartsWith(".NET SDKs installed:")) { inSdks = true; continue; }
                if (inSdks && string.IsNullOrWhiteSpace(line)) { inSdks = false; }
                if (inSdks && line.StartsWith("  "))
                {
                    sdkList.Add(line.Trim().Split(' ')[0]);
                }
            }
            if (sdkList.Any()) details["Installed SDKs"] = string.Join(", ", sdkList);
        }

        var yaml = $"dotnet:\n  sdk: \"{version.Output.Trim()}\"";
        if (details.ContainsKey("Installed SDKs") && details["Installed SDKs"].Contains("10.0"))
        {
            yaml += "\n  preview: true";
        }

        return new DiscoveryResult(".NET SDK", "Installed", version.Output.Trim(), details, yaml);
    }
}
