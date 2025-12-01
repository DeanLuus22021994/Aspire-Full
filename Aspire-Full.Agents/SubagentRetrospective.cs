namespace Aspire_Full.Agents;

public sealed record SubagentRetrospective(
    SubagentRole Role,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> NextSteps,
    DateTimeOffset Timestamp);
