using System.Text;
using System.Text.RegularExpressions;

namespace Aspire_Full.DockerRegistry;

internal sealed class DockerRegistryPatternEngine
{
    private static readonly Regex TokenRegex = new("\\{(?<token>[a-zA-Z0-9_]+)\\}", RegexOptions.Compiled);

    private readonly string _repositoryTemplate;
    private readonly string _tagTemplate;
    private readonly Regex _repositoryMatcher;

    public DockerRegistryPatternEngine(DockerRegistryPatternOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _repositoryTemplate = options.RepositoryTemplate;
        _tagTemplate = options.TagTemplate;
        _repositoryMatcher = BuildMatcher(_repositoryTemplate);
    }

    public string FormatRepository(DockerImageDescriptor descriptor, DockerRegistryPatternOptions options)
        => Format(_repositoryTemplate, descriptor, options);

    public string FormatTag(DockerImageDescriptor descriptor, DockerRegistryPatternOptions options)
        => Format(_tagTemplate, descriptor, options);

    public bool TryMatchRepository(string repository, out DockerImageDescriptor? descriptor)
    {
        var match = _repositoryMatcher.Match(repository);
        if (!match.Success)
        {
            descriptor = null;
            return false;
        }

        descriptor = new DockerImageDescriptor
        {
            Service = match.Groups.TryGetValue("service", out var service) && service.Success ? service.Value : repository,
            Environment = match.Groups.TryGetValue("environment", out var environment) && environment.Success ? environment.Value : null,
            Architecture = match.Groups.TryGetValue("architecture", out var architecture) && architecture.Success ? architecture.Value : null,
            Variant = match.Groups.TryGetValue("variant", out var variant) && variant.Success ? variant.Value : null
        };

        if (match.Groups.TryGetValue("version", out var version) && version.Success)
        {
            descriptor = descriptor with { Version = version.Value };
        }

        return true;
    }

    private static Regex BuildMatcher(string template)
    {
        var builder = new StringBuilder("^");
        var cursor = 0;
        foreach (Match tokenMatch in TokenRegex.Matches(template))
        {
            if (tokenMatch.Index > cursor)
            {
                var literal = Regex.Escape(template[cursor..tokenMatch.Index]);
                builder.Append(literal);
            }

            var tokenName = tokenMatch.Groups["token"].Value;
            builder.Append($"(?<{tokenName}>[\\w\\.-]+)");
            cursor = tokenMatch.Index + tokenMatch.Length;
        }

        if (cursor < template.Length)
        {
            builder.Append(Regex.Escape(template[cursor..]));
        }

        builder.Append("$");
        return new Regex(builder.ToString(), RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private static string Format(string template, DockerImageDescriptor descriptor, DockerRegistryPatternOptions options)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["namespace"] = options.Namespace,
            ["service"] = descriptor.Service,
            ["environment"] = descriptor.Environment ?? options.DefaultEnvironment,
            ["architecture"] = descriptor.Architecture ?? options.DefaultArchitecture,
            ["version"] = descriptor.Version ?? options.DefaultVersion
        };

        if (!string.IsNullOrWhiteSpace(descriptor.Variant ?? options.DefaultVariant))
        {
            values["variant"] = descriptor.Variant ?? options.DefaultVariant ?? string.Empty;
        }

        return TokenRegex.Replace(template, match =>
        {
            var key = match.Groups["token"].Value;
            return values.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }
}
