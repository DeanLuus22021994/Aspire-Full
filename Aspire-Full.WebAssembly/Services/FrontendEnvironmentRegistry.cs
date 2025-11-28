using Microsoft.Extensions.Options;

namespace Aspire_Full.WebAssembly.Services;

public sealed class FrontendEnvironmentRegistry
{
    private readonly IDictionary<string, FrontendEnvironmentDefinition> _definitions;
    private readonly string _defaultKey;

    public FrontendEnvironmentRegistry(IOptions<FrontendOptions> options)
    {
        var value = options.Value;
        _defaultKey = string.IsNullOrWhiteSpace(value.DefaultEnvironmentKey)
            ? FrontendEnvironmentKeys.DevelopmentDocs
            : value.DefaultEnvironmentKey;

        if (value.Environments.Count == 0)
        {
            value.Environments = BuildFallbackDefinitions();
        }

        _definitions = value.Environments.ToDictionary(def => def.Key, StringComparer.OrdinalIgnoreCase);
    }

    public FrontendEnvironmentDefinition GetDefault() => GetByKey(_defaultKey);

    public FrontendEnvironmentDefinition GetByKey(string key)
    {
        if (_definitions.TryGetValue(key, out var definition))
        {
            return definition;
        }

        return GetDefault();
    }

    public IReadOnlyCollection<FrontendEnvironmentDefinition> GetAll() => _definitions.Values.ToList();

    private static IList<FrontendEnvironmentDefinition> BuildFallbackDefinitions() =>
    [
        new FrontendEnvironmentDefinition
        {
            Key = FrontendEnvironmentKeys.DevelopmentDocs,
            DisplayName = "Development Docs",
            Description = "Live preview of docs + tooling running against local API",
            PrimaryColor = "#2dd4bf",
            BadgeText = "Docs",
            DocumentationUrl = "/docs",
            ApiBaseAddress = "http://localhost:5047"
        },
        new FrontendEnvironmentDefinition
        {
            Key = FrontendEnvironmentKeys.Uat,
            DisplayName = "UAT",
            Description = "Stakeholder validation environment mirroring production toggles",
            PrimaryColor = "#f97316",
            BadgeText = "UAT",
            DocumentationUrl = "https://uat.aspire-full.example/docs",
            ApiBaseAddress = "https://uat-api.aspire-full.example"
        },
        new FrontendEnvironmentDefinition
        {
            Key = FrontendEnvironmentKeys.Production,
            DisplayName = "Production",
            Description = "Hardened frontend connected to production services",
            PrimaryColor = "#22c55e",
            BadgeText = "Prod",
            DocumentationUrl = "https://docs.aspire-full.example",
            ApiBaseAddress = "https://api.aspire-full.example"
        }
    ];
}
