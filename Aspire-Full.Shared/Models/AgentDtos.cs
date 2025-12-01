using System.Text.Json.Serialization;
using Aspire_Full.Subagents;

namespace Aspire_Full.Shared.Models;

public sealed record AgentInput
{
    public SubagentRole Role { get; init; }
    public IReadOnlyList<string>? Completed { get; init; }
    public IReadOnlyList<string>? Risks { get; init; }
    public IReadOnlyList<string>? Next { get; init; }
    public IReadOnlyList<string>? Delegations { get; init; }
    public string? OutputPath { get; init; }
}

public sealed record AgentOutput(
    SubagentDefinition Definition,
    SubagentRetrospective Retrospective,
    SubagentDelegationPlan Delegation);
