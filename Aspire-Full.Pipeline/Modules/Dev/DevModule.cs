using System.CommandLine;

namespace Aspire_Full.Pipeline.Modules.Dev;

public class DevModule
{
    public Command GetCommand()
    {
        var command = new Command("dev", "Developer workflows");
        // Subcommands will be added here
        return command;
    }
}
