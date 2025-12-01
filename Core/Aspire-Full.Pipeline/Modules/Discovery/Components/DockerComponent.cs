using Aspire_Full.Pipeline.Modules.Discovery;
using Aspire_Full.Pipeline.Utils;
using System.Text.Json;

namespace Aspire_Full.Pipeline.Modules.Discovery.Components;

public class DockerComponent : IDiscoveryComponent
{
    public async Task<DiscoveryResult> DiscoverAsync(EnvironmentConfig config)
    {
        var version = await ProcessUtils.RunAsync("docker", ["version", "--format", "{{json .}}"]);
        if (version.ExitCode != 0)
            return new DiscoveryResult("Docker", "Missing", "docker command not found", new());

        var details = new Dictionary<string, string>();
        try
        {
            var info = await ProcessUtils.RunAsync("docker", ["info", "--format", "{{json .}}"]);
            if (info.ExitCode == 0)
            {
                using var doc = JsonDocument.Parse(info.Output);
                var root = doc.RootElement;
                if (root.TryGetProperty("ServerVersion", out var sv)) details["Server Version"] = sv.GetString() ?? "?";
                if (root.TryGetProperty("KernelVersion", out var kv)) details["Kernel"] = kv.GetString() ?? "?";
                if (root.TryGetProperty("MemTotal", out var mt)) details["MemTotal"] = (mt.GetInt64() / 1024 / 1024) + " MB";
                if (root.TryGetProperty("NCPU", out var cpu)) details["CPUs"] = cpu.GetInt32().ToString();
                if (root.TryGetProperty("Containers", out var c)) details["Containers"] = c.GetInt32().ToString();
                if (root.TryGetProperty("ContainersRunning", out var cr)) details["Running"] = cr.GetInt32().ToString();
            }
        }
        catch { /* Ignore parsing errors */ }

        var compose = await ProcessUtils.RunAsync("docker", ["compose", "version"]);
        if (compose.ExitCode == 0) details["Compose"] = compose.Output.Trim();

        config.Docker.Registry = "localhost:5000";
        config.Docker.ImagePrefix = "aspire-agents";

        return new DiscoveryResult("Docker", "Running", details.GetValueOrDefault("Server Version", "Unknown"), details);
    }
}
