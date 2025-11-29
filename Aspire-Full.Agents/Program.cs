using System.Text.Json;
using System.Text.Json.Serialization;
using Aspire_Full.Subagents;

var inputPath = GetOption("--input") ?? "subagents.update.json";
var outputPathOverride = GetOption("--output");

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file '{inputPath}' was not found.");
    return 1;
}

var serializerOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
};
serializerOptions.Converters.Add(new JsonStringEnumConverter());

var agentInput = JsonSerializer.Deserialize<AgentInput>(File.ReadAllText(inputPath), serializerOptions);
if (agentInput is null)
{
    Console.Error.WriteLine("Unable to parse agent input.");
    return 1;
}

var update = SubagentUpdate.Normalize(
    agentInput.Role,
    agentInput.Completed,
    agentInput.Risks,
    agentInput.Next,
    agentInput.Delegations);

var service = new SubagentSelfReviewService();
var definition = service.GetDefinition(update.Role);
var retrospective = service.CreateRetrospective(update);
var delegationPlan = service.CreateDelegationPlan(update);

var agentOutput = new AgentOutput(definition, retrospective, delegationPlan);
var outputPath = outputPathOverride ?? agentInput.OutputPath ?? Path.ChangeExtension(inputPath, ".output.json");

File.WriteAllText(outputPath, JsonSerializer.Serialize(agentOutput, serializerOptions));

Console.WriteLine($"Subagent retrospective generated for {definition.Name}.");
Console.WriteLine($"Highlights: {string.Join(", ", retrospective.Highlights)}");
Console.WriteLine($"Risks: {string.Join(", ", retrospective.Risks)}");
Console.WriteLine($"Next: {string.Join(", ", retrospective.NextSteps)}");
Console.WriteLine($"Delegations: {delegationPlan.Items.Count}");
Console.WriteLine($"Output saved to {outputPath}");

return 0;

string? GetOption(string name)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }

    return null;
}

internal sealed record AgentInput
{
    public SubagentRole Role { get; init; }
    public IReadOnlyList<string>? Completed { get; init; }
    public IReadOnlyList<string>? Risks { get; init; }
    public IReadOnlyList<string>? Next { get; init; }
    public IReadOnlyList<string>? Delegations { get; init; }
    public string? OutputPath { get; init; }
}

internal sealed record AgentOutput(
    SubagentDefinition Definition,
    SubagentRetrospective Retrospective,
    SubagentDelegationPlan Delegation);
