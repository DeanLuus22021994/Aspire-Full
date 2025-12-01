namespace Aspire_Full.Agents;

public sealed record SubagentDelegationPlan(
    SubagentRole Role,
    IReadOnlyList<DelegatedWorkItem> Items,
    DateTimeOffset Timestamp);
