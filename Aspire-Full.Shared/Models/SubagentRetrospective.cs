namespace Aspire_Full.Shared.Models;

public sealed record SubagentRetrospective(
    SubagentRole Role,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> NextSteps,
    DateTimeOffset Timestamp);
