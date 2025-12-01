namespace Aspire_Full.Pipeline.Constants;

public static class EnvConstants
{
    public static readonly Dictionary<string, string> AspireAppHost = new()
    {
        ["DOTNET_EnableAVX2"] = "1",
        ["DOTNET_EnableSSE41"] = "1",
        ["DOTNET_TieredPGO"] = "1",
        ["DOTNET_TieredCompilation"] = "1",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["DOTNET_NOLOGO"] = "1",
        ["CUDA_VISIBLE_DEVICES"] = "all",
        ["TF_FORCE_GPU_ALLOW_GROWTH"] = "true",
        ["NVIDIA_VISIBLE_DEVICES"] = "all",
        ["NVIDIA_DRIVER_CAPABILITIES"] = "compute,utility",
        ["NVIDIA_REQUIRE_CUDA"] = "cuda>=12.4,driver>=535",
        ["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] = "true",
        ["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "http://localhost:18889",
        ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:18889"
    };

    public static readonly Dictionary<string, string> BuildAndTest = new()
    {
        ["DOTNET_EnableAVX2"] = "1",
        ["DOTNET_EnableSSE41"] = "1",
        ["DOTNET_TieredPGO"] = "1",
        ["DOTNET_ReadyToRun"] = "1",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["DOTNET_NOLOGO"] = "1",
        ["CUDA_VISIBLE_DEVICES"] = "all",
        ["NVIDIA_VISIBLE_DEVICES"] = "all",
        ["NVIDIA_DRIVER_CAPABILITIES"] = "compute,utility",
        ["NVIDIA_REQUIRE_CUDA"] = "cuda>=12.4,driver>=535",
        ["TF_FORCE_GPU_ALLOW_GROWTH"] = "true"
    };
}
