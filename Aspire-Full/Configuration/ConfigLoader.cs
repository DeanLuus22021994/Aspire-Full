using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Aspire_Full.Configuration;

public static class ConfigLoader
{
    public static Settings LoadSettings(string path)
    {
        if (!File.Exists(path))
        {
            return new Settings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
    }

    public static RuntimeConfig LoadRuntimeConfig(string path)
    {
        if (!File.Exists(path))
        {
            return new RuntimeConfig();
        }

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<RuntimeConfig>(yaml);
    }
}
