using System.CommandLine;

namespace Aspire_Full.Pipeline.Modules.Ai;

public class AiModule
{
    public Command GetCommand()
    {
        var command = new Command("ai", "AI and Agent workflows");
        // Subcommands will be added here
        return command;
    }
}
