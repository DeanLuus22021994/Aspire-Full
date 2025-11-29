using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Aspire_Full.DockerRegistry.Configuration;
using Aspire_Full.DockerRegistry.Models;

namespace Aspire_Full.DockerRegistry.Services;

internal sealed class DockerRegistryPatternEngine
{
    private static readonly Regex TokenRegex = new("\\{(?<token>[a-zA-Z0-9_]+)\\}", RegexOptions.Compiled);

    private readonly string _repositoryTemplate;
    private readonly string _tagTemplate;
    private readonly Regex _repositoryMatcher;
    private readonly HashSet<string> _repositoryTokens;

    public DockerRegistryPatternEngine(DockerRegistryPatternOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _repositoryTemplate = options.RepositoryTemplate;
        _tagTemplate = options.TagTemplate;
        (_repositoryMatcher, _repositoryTokens) = BuildMatcher(_repositoryTemplate);
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

        var groups = match.Groups;
        descriptor = new DockerImageDescriptor
        {
            Service = TryGetGroupValue(groups, "service", out var service) ? service! : repository,
            Environment = TryGetGroupValue(groups, "environment", out var environment) ? environment : null,
            Architecture = TryGetGroupValue(groups, "architecture", out var architecture) ? architecture : null,
            Variant = TryGetGroupValue(groups, "variant", out var variant) ? variant : null,
            Version = TryGetGroupValue(groups, "version", out var version) ? version : null
        };

        return true;
    }

    private static (Regex regex, HashSet<string> tokens) BuildMatcher(string template)
    {
        var builder = new StringBuilder("^");
        var cursor = 0;
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match tokenMatch in TokenRegex.Matches(template))
        {
            if (tokenMatch.Index > cursor)
            {
                var literal = Regex.Escape(template[cursor..tokenMatch.Index]);
                builder.Append(literal);
            }

            var tokenName = tokenMatch.Groups["token"].Value;
            tokens.Add(tokenName);
            builder.Append($"(?<{tokenName}>[\\w\\.-]+)");
            cursor = tokenMatch.Index + tokenMatch.Length;
        }

        if (cursor < template.Length)
        {
            builder.Append(Regex.Escape(template[cursor..]));
        }

        builder.Append("$");
        var regex = new Regex(builder.ToString(), RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return (regex, tokens);
    }

    private bool TryGetGroupValue(GroupCollection groups, string token, out string? value)
    {
        if (!_repositoryTokens.Contains(token))
        {
            value = null;
            return false;
        }

        var group = groups[token];
        if (group.Success)
        {
            value = group.Value;
            return true;
        }

        value = null;
        return false;
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
