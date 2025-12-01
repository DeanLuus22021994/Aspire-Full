using System.Linq;

namespace Aspire_Full.Agents;

public sealed class SubagentSelfReviewService
{
    private readonly Func<DateTimeOffset> _clock;

    public SubagentSelfReviewService()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public SubagentSelfReviewService(Func<DateTimeOffset> clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public SubagentDefinition GetDefinition(SubagentRole role) => SubagentCatalog.Get(role);

    public SubagentRetrospective CreateRetrospective(SubagentUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var highlights = update.Completed.Any()
            ? update.Completed
            : new[] { "No highlights were submitted." };

        var risks = update.Risks.Any()
            ? update.Risks
            : new[] { "No risks captured." };

        var next = update.Next.Any()
            ? update.Next
            : new[] { "Define explicit next steps." };

        return new SubagentRetrospective(update.Role, highlights, risks, next, _clock());
    }

    public SubagentDelegationPlan CreateDelegationPlan(SubagentUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var items = update.Delegations.Any()
            ? update.Delegations.Select(d => new DelegatedWorkItem(update.Role, d, InferPriority(d))).ToArray()
            : Array.Empty<DelegatedWorkItem>();

        return new SubagentDelegationPlan(update.Role, items, _clock());
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
