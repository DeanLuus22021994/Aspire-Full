namespace Aspire_Full.WebAssembly.Services;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string DefaultEnvironmentKey { get; set; } = FrontendEnvironmentKeys.DevelopmentDocs;

    public IList<FrontendEnvironmentDefinition> Environments { get; set; } = new List<FrontendEnvironmentDefinition>();
}

public static class FrontendEnvironmentKeys
{
    public const string DevelopmentDocs = "docs";
    public const string Uat = "uat";
    public const string Production = "prod";
}

public sealed class FrontendEnvironmentDefinition
{
    public required string Key { get; set; }
    public required string DisplayName { get; set; }
    public string Description { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#3b82f6";
    public string DocumentationUrl { get; set; } = string.Empty;
    public string ApiBaseAddress { get; set; } = "http://localhost:5047";
    public string BadgeText { get; set; } = string.Empty;
}
