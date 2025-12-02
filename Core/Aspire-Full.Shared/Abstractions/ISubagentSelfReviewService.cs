namespace Aspire_Full.Shared.Abstractions;

using Aspire_Full.Shared.Models;

/// <summary>
/// Service for generating subagent retrospectives and delegation plans.
/// AI agents can use this interface to produce structured self-review outputs.
/// </summary>
public interface ISubagentSelfReviewService
{
    /// <summary>
    /// Gets the definition for a specific subagent role.
    /// </summary>
    SubagentDefinition GetDefinition(SubagentRole role);

    /// <summary>
    /// Creates a retrospective from a subagent update.
    /// </summary>
    SubagentRetrospective CreateRetrospective(SubagentUpdate update);

    /// <summary>
    /// Creates a delegation plan from a subagent update.
    /// </summary>
    SubagentDelegationPlan CreateDelegationPlan(SubagentUpdate update);
}

/// <summary>
/// Represents an update from a subagent for self-review processing.
/// </summary>
public sealed record SubagentUpdate(
    SubagentRole Role,
    IReadOnlyList<string> Completed,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Next,
    IReadOnlyList<string> Delegations)
{
    /// <summary>
    /// Normalizes raw input into a clean SubagentUpdate.
    /// </summary>
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
            .ToArray() ?? [];
    }
}
