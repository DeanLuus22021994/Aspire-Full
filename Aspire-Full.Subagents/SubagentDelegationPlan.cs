namespace Aspire_Full.Subagents;

public sealed record SubagentDelegationPlan(
    SubagentRole Role,
    IReadOnlyList<DelegatedWorkItem> Items,
    DateTimeOffset Timestamp);
