namespace Aspire_Full.Pipeline.Modules.Discovery;

public record DiscoveryResult(string Category, string Status, string Summary, Dictionary<string, string> Details, string? RecommendedYaml = null);

public interface IDiscoveryComponent
{
    Task<DiscoveryResult> DiscoverAsync(EnvironmentConfig config);
}
