using System.Text.RegularExpressions;

namespace Aspire_Full.Pipeline.Utils;

public static class DockerUtils
{
    public static async Task<bool> IsDockerRunningAsync()
    {
        try
        {
            var result = await ProcessUtils.RunAsync("docker", ["info"]);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> NetworkExistsAsync(string networkName)
    {
        var result = await ProcessUtils.RunAsync("docker", ["network", "ls", "-q", "--filter", $"name=^{networkName}$"]);
        return !string.IsNullOrWhiteSpace(result.Output);
    }

    public static async Task CreateNetworkAsync(string networkName)
    {
        await ProcessUtils.RunAsync("docker", ["network", "create", networkName]);
    }

    public static async Task<bool> VolumeExistsAsync(string volumeName)
    {
        var result = await ProcessUtils.RunAsync("docker", ["volume", "ls", "-q", "--filter", $"name=^{volumeName}$"]);
        return !string.IsNullOrWhiteSpace(result.Output);
    }

    public static async Task CreateVolumeAsync(string volumeName)
    {
        await ProcessUtils.RunAsync("docker", ["volume", "create", volumeName]);
    }
}
