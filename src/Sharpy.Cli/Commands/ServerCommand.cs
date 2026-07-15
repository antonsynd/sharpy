using System.CommandLine;
using Sharpy.Cli.Services;

namespace Sharpy.Cli.Commands;

/// <summary>
/// <c>sharpyc server</c> — start a keep-alive compile server on a named pipe (D2, #1049). The
/// server is started explicitly (no daemon auto-spawn); clients opt in with
/// <c>sharpyc build|run --server[=NAME]</c> and fall back to an in-process compile if none is
/// running.
/// </summary>
internal static class ServerCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("server", "Start a keep-alive compile server on a named pipe (warm subsequent compiles)");

        var pipeOpt = new Option<string?>("--pipe")
        {
            Description = $"Named pipe to listen on (default: {CompileServerProtocol.DefaultPipeName})",
        };
        var idleOpt = new Option<int>("--idle-timeout")
        {
            Description = "Shut down after this many seconds with no client connection (default 300; 0 disables the timeout)",
            DefaultValueFactory = _ => 300,
        };

        command.Options.Add(pipeOpt);
        command.Options.Add(idleOpt);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var pipeName = parseResult.GetValue(pipeOpt) ?? CompileServerProtocol.DefaultPipeName;
            var idleSeconds = parseResult.GetValue(idleOpt);
            var logLevel = globals.ResolveLogLevel(parseResult);
            var logFile = parseResult.GetValue(globals.LogFile);
            var logger = CliHelpers.CreateLogger(logLevel, logFile);

            var idleTimeout = idleSeconds <= 0
                ? Timeout.InfiniteTimeSpan
                : TimeSpan.FromSeconds(idleSeconds);

            var server = new CompileServer(pipeName, idleTimeout, logger);
            return await server.RunAsync(cancellationToken).ConfigureAwait(false);
        });

        root.Subcommands.Add(command);
    }
}
