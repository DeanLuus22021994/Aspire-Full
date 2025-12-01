using System.CommandLine;
using Aspire_Full.Pipeline.Services;
using Aspire_Full.Pipeline.Constants;

namespace Aspire_Full.Pipeline.Modules.Docs;

public class DocsModule
{
    private readonly DocsService _service = new();

    public Command GetCommand()
    {
        var command = new Command(CommandConstants.Docs.Name, CommandConstants.Docs.Description);

        // Generate
        var generateCommand = new Command(CommandConstants.Docs.Generate, "Generate documentation");
        var apiOption = new Option<bool>(["--api", "-a"], "Generate API documentation");
        var llmsOption = new Option<bool>(["--llms", "-l"], "Update llms.txt");
        var allOption = new Option<bool>(["--all"], "Generate all documentation");

        generateCommand.AddOption(apiOption);
        generateCommand.AddOption(llmsOption);
        generateCommand.AddOption(allOption);

        generateCommand.SetHandler(async (api, llms, all) =>
            await _service.GenerateDocsAsync(api, llms, all),
            apiOption, llmsOption, allOption);
        command.AddCommand(generateCommand);

        // Changelog
        var changelogCommand = new Command(CommandConstants.Docs.Changelog, "Generate changelog");
        var versionOption = new Option<string>(["--next-version", "-v"], "Next version number");

        changelogCommand.AddOption(versionOption);
        changelogCommand.SetHandler(async (version) =>
            await _service.GenerateChangelogAsync(version),
            versionOption);
        command.AddCommand(changelogCommand);

        return command;
    }
}
