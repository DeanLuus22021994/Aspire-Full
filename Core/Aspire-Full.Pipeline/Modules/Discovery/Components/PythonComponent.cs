using Aspire_Full.Pipeline.Modules.Discovery;
using Aspire_Full.Pipeline.Utils;
using System.Text.Json;
using Aspire_Full.Shared;

namespace Aspire_Full.Pipeline.Modules.Discovery.Components;

public class PythonComponent : IDiscoveryComponent
{
    public async Task<DiscoveryResult> DiscoverAsync(EnvironmentConfig config)
    {
        string root;
        try { root = RepoComponent.LocateRepositoryRoot(); } catch { root = Directory.GetCurrentDirectory(); }

        var venvPython = Path.Combine(root, "Aspire-Full.Python", "python-agents", ".venv", "Scripts", "python.exe");
        string pythonExe = "python";
        string type = "Global";

        if (File.Exists(venvPython))
        {
            pythonExe = venvPython;
            type = "VirtualEnv";
        }

        var script = """
import sys, platform, json
try:
    import torch
    torch_ver = torch.__version__
    cuda = torch.cuda.is_available()
    cuda_ver = torch.version.cuda if cuda else None
except ImportError:
    torch_ver = "Not Installed"
    cuda = False
    cuda_ver = None

info = {
    "version": platform.python_version(),
    "implementation": platform.python_implementation(),
    "compiler": platform.python_compiler(),
    "free_threading": bool(sys.flags.nogil) if hasattr(sys.flags, 'nogil') else False,
    "torch": torch_ver,
    "cuda_available": cuda,
    "cuda_version": cuda_ver,
    "executable": sys.executable
}
print(json.dumps(info))
""";

        var tempFile = Path.GetTempFileName() + ".py";
        await File.WriteAllTextAsync(tempFile, script);

        try
        {
            var result = await ProcessUtils.RunAsync(pythonExe, [tempFile], root);
            if (result.ExitCode != 0)
            {
                // Fallback if python not found
                return new DiscoveryResult("Python", "Missing", "Python executable not found or failed to run", new());
            }

            try
            {
                var data = JsonSerializer.Deserialize(result.Output.Trim(), AppJsonContext.Default.DictionaryStringObject);
                var details = data?.ToDictionary(k => k.Key, k => k.Value?.ToString() ?? "null") ?? new();

                // Check UV separately
                var uvCheck = await ProcessUtils.RunAsync("uv", ["--version"], root);
                details["uv"] = uvCheck.ExitCode == 0 ? uvCheck.Output.Trim() : "Missing";

                var summary = $"{details["version"]} ({type})";
                if (details.TryGetValue("free_threading", out var ft) && ft.Equals("True", StringComparison.OrdinalIgnoreCase))
                    summary += " [Free-Threading]";

                config.Python.Version = details["version"];
                config.Python.Manager = "uv";

                if (details.TryGetValue("cuda_available", out var cuda) && cuda.Equals("True", StringComparison.OrdinalIgnoreCase))
                {
                    config.Python.Torch.Device = "cuda";
                }
                else
                {
                    config.Python.Torch.Device = "cpu";
                }

                return new DiscoveryResult("Python", "Found", summary, details);
            }
            catch (Exception ex)
            {
                return new DiscoveryResult("Python", "Error", $"Failed to parse output: {ex.Message}", new() { ["Output"] = result.Output });
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
