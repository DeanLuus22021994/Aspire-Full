using Aspire_Full.Pipeline.Modules.Discovery;
using Aspire_Full.Pipeline.Utils;

namespace Aspire_Full.Pipeline.Modules.Discovery.Components;

public class HardwareComponent : IDiscoveryComponent
{
    public async Task<DiscoveryResult> DiscoverAsync(EnvironmentConfig config)
    {
        var details = new Dictionary<string, string>();

        // GPU
        try
        {
            var gpu = await ProcessUtils.RunAsync("nvidia-smi", ["--query-gpu=name,driver_version,memory.total,utilization.gpu", "--format=csv,noheader"]);
            if (gpu.ExitCode == 0 && !string.IsNullOrWhiteSpace(gpu.Output))
            {
                var parts = gpu.Output.Trim().Split(',');
                if (parts.Length >= 3)
                {
                    details["GPU Name"] = parts[0].Trim();
                    details["Driver"] = parts[1].Trim();
                    details["VRAM"] = parts[2].Trim();
                    if (parts.Length > 3) details["Utilization"] = parts[3].Trim();
                }
                else
                {
                    details["GPU Raw"] = gpu.Output.Trim();
                }

                config.Hardware.Gpu.Enabled = true;
                config.Hardware.Gpu.Driver = details.GetValueOrDefault("Driver", "latest");

                return new DiscoveryResult("Hardware", "GPU Available", details.GetValueOrDefault("GPU Name", "NVIDIA GPU"), details);
            }
        }
        catch { }

        config.Hardware.Gpu.Enabled = false;
        return new DiscoveryResult("Hardware", "No GPU", "Standard CPU Environment", details);
    }
}
