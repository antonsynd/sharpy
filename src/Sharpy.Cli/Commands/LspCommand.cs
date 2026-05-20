using System.CommandLine;

namespace Sharpy.Cli.Commands;

internal static class LspCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("lsp", "Start the Sharpy Language Server (stdio transport)");
        var stdioOpt = new Option<bool>("--stdio") { Description = "Use stdio transport (default, accepted for compatibility)" };
        command.Options.Add(stdioOpt);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            await Sharpy.Lsp.Program.Main(Array.Empty<string>()).ConfigureAwait(false);
        });

        root.Subcommands.Add(command);
    }
}
