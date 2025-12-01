using System.CommandLine;
using Aspire_Full.Pipeline.Constants;
using Aspire_Full.Pipeline.Services;

namespace Aspire_Full.Pipeline.Modules.Infra;

public class InfraModule
{
    private readonly InfraService _service = new();

    public Command GetCommand()
    {
        var command = new Command(CommandConstants.Infra.Name, CommandConstants.Infra.Description);

        var initCommand = new Command(CommandConstants.Infra.Init, CommandConstants.Infra.InitDesc);
        initCommand.SetHandler(async () => await _service.InitInfrastructureAsync());
        command.AddCommand(initCommand);

        return command;
    }
}
