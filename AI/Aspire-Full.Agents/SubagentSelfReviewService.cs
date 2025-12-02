using Aspire_Full.Shared.Abstractions;
using Aspire_Full.Shared.Models;

namespace Aspire_Full.Agents;

public sealed class SubagentSelfReviewService : ISubagentSelfReviewService
{
    private readonly TimeProvider _timeProvider;

    public SubagentSelfReviewService()
        : this(TimeProvider.System)
    {
    }

    public SubagentSelfReviewService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public SubagentDefinition GetDefinition(SubagentRole role) => SubagentCatalog.Get(role);

    public SubagentRetrospective CreateRetrospective(SubagentUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var highlights = update.Completed.Count > 0
            ? update.Completed
            : ["No highlights were submitted."];

        var risks = update.Risks.Count > 0
            ? update.Risks
            : ["No risks captured."];

        var next = update.Next.Count > 0
            ? update.Next
            : ["Define explicit next steps."];

        return new SubagentRetrospective(update.Role, highlights, risks, next, _timeProvider.GetUtcNow());
    }

    public SubagentDelegationPlan CreateDelegationPlan(SubagentUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var items = update.Delegations.Count > 0
            ? update.Delegations.Select(d => new DelegatedWorkItem(update.Role, d, InferPriority(d))).ToArray()
            : [];

        return new SubagentDelegationPlan(update.Role, items, _timeProvider.GetUtcNow());
    }

    private static DelegationPriority InferPriority(string description)
    {
        if (description.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("blocker", StringComparison.OrdinalIgnoreCase))
        {
            return DelegationPriority.High;
        }

        if (description.Contains("doc", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("review", StringComparison.OrdinalIgnoreCase))
        {
            return DelegationPriority.Medium;
        }

        return DelegationPriority.Low;
    }
}
