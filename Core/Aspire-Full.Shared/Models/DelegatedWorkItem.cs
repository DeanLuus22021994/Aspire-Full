namespace Aspire_Full.Shared.Models;

public sealed record DelegatedWorkItem(
    SubagentRole Role,
    string Description,
    DelegationPriority Priority);

public enum DelegationPriority
{
    Low,
    Medium,
    High,
}
