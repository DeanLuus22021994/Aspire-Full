using System.CommandLine;
using Aspire_Full.Pipeline.Modules.Ai;
using Aspire_Full.Pipeline.Modules.Ci;
using Aspire_Full.Pipeline.Modules.Dev;
using Aspire_Full.Pipeline.Modules.Discovery;
using Aspire_Full.Pipeline.Modules.Docs;
using Aspire_Full.Pipeline.Modules.Infra;
using Aspire_Full.Pipeline.Modules.Pipeline;

Console.Title = "Aspire-Full.Pipeline";

var rootCommand = new RootCommand("Aspire-Full Pipeline Tool");

// Register Modules
rootCommand.AddCommand(new DiscoveryModule().GetCommand());
rootCommand.AddCommand(new InfraModule().GetCommand());
rootCommand.AddCommand(new CiModule().GetCommand());
rootCommand.AddCommand(new DevModule().GetCommand());
rootCommand.AddCommand(new AiModule().GetCommand());
rootCommand.AddCommand(new DocsModule().GetCommand());
rootCommand.AddCommand(new PipelineModule().GetCommand());

return await rootCommand.InvokeAsync(args);
