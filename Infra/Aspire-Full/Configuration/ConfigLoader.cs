using System.Text.Json;
using Aspire_Full.Shared;
using Aspire_Full.Shared.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using Settings = Aspire_Full.Shared.Models.Settings;

namespace Aspire_Full.Configuration;

public static class ConfigLoader
{
    private const string UnifiedConfigPath = ".config/aspire-config.yaml";
    private const string LegacyRuntimeConfigPath = ".config/config.yaml";

    private static readonly Lazy<IDeserializer> s_yamlDeserializer = new(() =>
        new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build());

    public static Settings LoadSettings(string path)
    {
        if (!File.Exists(path))
        {
            return new Settings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, AppJsonContext.Default.Settings) ?? new Settings();
    }

    /// <summary>
    /// Loads the unified Aspire configuration from aspire-config.yaml.
    /// Falls back to legacy config.yaml if unified config doesn't exist.
    /// </summary>
    public static AspireConfig LoadAspireConfig(string? basePath = null)
    {
        basePath ??= Directory.GetCurrentDirectory();

        // Try unified config first
        var unifiedPath = Path.Combine(basePath, UnifiedConfigPath);
        if (File.Exists(unifiedPath))
        {
            var yaml = File.ReadAllText(unifiedPath);
            return s_yamlDeserializer.Value.Deserialize<AspireConfig>(yaml);
        }

        // Fall back to legacy config
        var legacyPath = Path.Combine(basePath, LegacyRuntimeConfigPath);
        if (File.Exists(legacyPath))
        {
            var runtimeConfig = LoadRuntimeConfig(legacyPath);
            return ConvertLegacyConfig(runtimeConfig);
        }

        return new AspireConfig();
    }

    /// <summary>
    /// Loads legacy runtime config for backward compatibility.
    /// </summary>
    public static RuntimeConfig LoadRuntimeConfig(string path)
    {
        if (!File.Exists(path))
        {
            return new RuntimeConfig();
        }

        var yaml = File.ReadAllText(path);
        return s_yamlDeserializer.Value.Deserialize<RuntimeConfig>(yaml);
    }

    /// <summary>
    /// Converts legacy RuntimeConfig to new AspireConfig format.
    /// </summary>
    private static AspireConfig ConvertLegacyConfig(RuntimeConfig legacy)
    {
        var config = new AspireConfig
        {
            DotNet = legacy.Environment.DotNet,
            Python = legacy.Environment.Python,
            Hardware = legacy.Environment.Hardware,
            Telemetry = legacy.Telemetry
        };

        // Map legacy GPU snapshot to new telemetry config
        if (legacy.Telemetry.Gpu.Snapshot != null)
        {
            config.Telemetry.Gpu.TargetUtilization = (int)legacy.Telemetry.Gpu.Snapshot.TargetUtilization;
        }

        return config;
    }
}
