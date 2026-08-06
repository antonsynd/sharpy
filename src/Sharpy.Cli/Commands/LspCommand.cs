using System.CommandLine;
using Sharpy.Compiler.Logging;

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
            // The global --log-level is recursive, so `sharpyc lsp --log-level Debug` parses here
            // and would otherwise be swallowed: forward it to the server, which is the process that
            // does the logging (#1225). Unset means the server applies its own resolution
            // (SHARPY_LSP_LOG_LEVEL, then Information), so an absent flag stays absent rather than
            // becoming an explicit "None".
            var level = parseResult.GetValue(globals.LogLevel);
            var args = level is { } value
                ? new[] { Sharpy.Lsp.ServerCommandLine.LogLevelFlag, ToServerLogLevel(value) }
                : Array.Empty<string>();

            await Sharpy.Lsp.Program.Main(args).ConfigureAwait(false);
        });

        root.Subcommands.Add(command);
    }

    /// <summary>
    /// Maps the CLI's <see cref="CompilerLogLevel"/> onto the
    /// <see cref="Microsoft.Extensions.Logging.LogLevel"/> name the server parses. Only the two
    /// spellings that differ need mapping; the server keeps strict level names so there is one
    /// vocabulary per process rather than a growing alias list.
    /// </summary>
    private static string ToServerLogLevel(CompilerLogLevel level) => level switch
    {
        CompilerLogLevel.Info => nameof(Microsoft.Extensions.Logging.LogLevel.Information),
        CompilerLogLevel.Trace => nameof(Microsoft.Extensions.Logging.LogLevel.Trace),
        _ => level.ToString(),
    };
}
