using System.Linq;

namespace Aspire_Full.Subagents;

public sealed record SubagentUpdate(
    SubagentRole Role,
    IReadOnlyList<string> Completed,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Next,
    IReadOnlyList<string> Delegations)
{
    public static SubagentUpdate Normalize(
        SubagentRole role,
        IEnumerable<string>? completed,
        IEnumerable<string>? risks,
        IEnumerable<string>? next,
        IEnumerable<string>? delegations)
    {
        return new SubagentUpdate(
            role,
            NormalizeList(completed),
            NormalizeList(risks),
            NormalizeList(next),
            NormalizeList(delegations));
    }

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? values)
    {
        return values?.Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .ToArray() ?? Array.Empty<string>();
    }
}
