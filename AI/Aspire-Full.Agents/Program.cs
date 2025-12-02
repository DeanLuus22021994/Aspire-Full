using System.Text.Json;
using Aspire_Full.Agents.Core.Catalog;
using Aspire_Full.Agents.Core.Maintenance;
using Aspire_Full.Agents.Core.Services;
using Aspire_Full.Shared;
using Aspire_Full.Shared.Abstractions;
using Aspire_Full.Shared.Models;

var maintenanceMode = GetOption("--maintenance");
if (maintenanceMode != null)
{
    var workspace = GetOption("--workspace") ?? Directory.GetCurrentDirectory();
    IMaintenanceAgent agent = new MaintenanceAgent();
    var result = await agent.RunAsync(workspace);
    if (!result.IsSuccess)
    {
        Console.Error.WriteLine($"❌ Maintenance failed: {result.Error}");
        return 1;
    }
    Console.WriteLine($"✅ Maintenance complete. Tasks: {string.Join(", ", result.Value!.ExecutedTasks)}");
    Console.WriteLine($"⏱️ Duration: {result.Value.Duration}");
    return 0;
}

var inputPath = GetOption("--input") ?? "subagents.update.json";
var outputPathOverride = GetOption("--output");

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file '{inputPath}' was not found.");
    return 1;
}

var agentInput = JsonSerializer.Deserialize(File.ReadAllText(inputPath), AppJsonContext.Default.AgentInput);
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

// Use Infra implementations
ISubagentCatalog catalog = new SubagentCatalog();
ISubagentSelfReviewService service = new SubagentSelfReviewService(catalog);
var definition = service.GetDefinition(update.Role);
var retrospective = service.CreateRetrospective(update);
var delegationPlan = service.CreateDelegationPlan(update);

var agentOutput = new AgentOutput(definition, retrospective, delegationPlan);
var outputPath = outputPathOverride ?? agentInput.OutputPath ?? Path.ChangeExtension(inputPath, ".output.json");

File.WriteAllText(outputPath, JsonSerializer.Serialize(agentOutput, AppJsonContext.Default.AgentOutput));

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
