using System.CommandLine;
using Aspire_Full.Pipeline.Services;
using Aspire_Full.Pipeline.Constants;

namespace Aspire_Full.Pipeline.Modules.Discovery;

public class DiscoveryModule
{
    private readonly DiscoveryService _service = new();

    public Command GetCommand()
    {
        var command = new Command(CommandConstants.Discover.Name, CommandConstants.Discover.Description);

        command.SetHandler(async () => await _service.RunDiscoveryAsync());

        return command;
    }
}
