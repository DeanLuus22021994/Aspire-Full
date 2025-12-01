using System.CommandLine;

namespace Aspire_Full.Pipeline.Modules.Docs;

public class DocsModule
{
    public Command GetCommand()
    {
        var command = new Command("docs", "Documentation management");
        // Subcommands will be added here
        return command;
    }
}
