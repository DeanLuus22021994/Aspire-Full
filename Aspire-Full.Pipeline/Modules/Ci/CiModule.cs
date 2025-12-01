using System.CommandLine;

namespace Aspire_Full.Pipeline.Modules.Ci;

public class CiModule
{
    public Command GetCommand()
    {
        var command = new Command("ci", "Manage CI/CD workflows");
        // Subcommands will be added here
        return command;
    }
}
