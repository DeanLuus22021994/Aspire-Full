namespace Aspire_Full.Subagents;

public sealed record SubagentRetrospective(
    SubagentRole Role,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> NextSteps,
    DateTimeOffset Timestamp);
