namespace Aspire_Full.Shared.Models;

public sealed record SubagentDelegationPlan(
    SubagentRole Role,
    IReadOnlyList<DelegatedWorkItem> Items,
    DateTimeOffset Timestamp);
