using System.Collections.ObjectModel;
using System.Linq;

namespace Aspire_Full.Subagents;

public sealed record SubagentDefinition
{
    public required SubagentRole Role { get; init; }
    public required string Name { get; init; }
    public required string Directory { get; init; }
    public required string Mission { get; init; }
    public IReadOnlyList<string> Inputs { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Outputs { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Interfaces { get; init; } = Array.Empty<string>();
    public string UiPage { get; init; } = string.Empty;

    public SubagentDefinition WithLists(
        IEnumerable<string>? inputs = null,
        IEnumerable<string>? outputs = null,
        IEnumerable<string>? constraints = null,
        IEnumerable<string>? interfaces = null)
    {
        return this with
        {
            Inputs = new ReadOnlyCollection<string>((inputs ?? Array.Empty<string>()).ToList()),
            Outputs = new ReadOnlyCollection<string>((outputs ?? Array.Empty<string>()).ToList()),
            Constraints = new ReadOnlyCollection<string>((constraints ?? Array.Empty<string>()).ToList()),
            Interfaces = new ReadOnlyCollection<string>((interfaces ?? Array.Empty<string>()).ToList()),
        };
    }
}
