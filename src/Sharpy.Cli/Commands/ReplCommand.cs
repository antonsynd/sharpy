using System.CommandLine;
using System.Text;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Services;

namespace Sharpy.Cli.Commands;

internal static class ReplCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("repl", "Start an interactive Sharpy REPL");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            return await RunReplAsync(logger, cancellationToken).ConfigureAwait(false);
        });

        root.Subcommands.Add(command);
    }

    static async Task<int> RunReplAsync(ICompilerLogger logger, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Sharpy REPL ({VersionInfo.GetDisplayString()})");
        Console.WriteLine("Type 'exit()' or press Ctrl+D to quit. End a line with ':' to start a multi-line block.");

        var session = new ReplSession(logger);
        var pending = new StringBuilder();
        var inMultiLine = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Console.Write(inMultiLine ? "... " : ">>> ");
            }
            catch (IOException)
            {
                return 0;
            }

            string? line;
            try
            {
                line = Console.ReadLine();
            }
            catch (IOException)
            {
                line = null;
            }

            if (line == null)
            {
                Console.WriteLine();
                return 0;
            }

            if (inMultiLine)
            {
                if (line.Length == 0)
                {
                    var block = pending.ToString();
                    pending.Clear();
                    inMultiLine = false;

                    if (block.Trim().Length > 0)
                    {
                        await ExecuteAndDisplayAsync(session, block, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    pending.AppendLine(line);
                }
                continue;
            }

            if (line.TrimEnd().Length == 0)
                continue;

            var trimmedForExit = line.Trim();
            if (trimmedForExit == "exit()" || trimmedForExit == "quit()")
            {
                return 0;
            }

            if (LineStartsBlock(line))
            {
                pending.AppendLine(line);
                inMultiLine = true;
                continue;
            }

            await ExecuteAndDisplayAsync(session, line, cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }

    static async Task ExecuteAndDisplayAsync(ReplSession session, string input, CancellationToken cancellationToken)
    {
        ReplResult result;
        try
        {
            result = await session.EvaluateAsync(input, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"REPL error: {ex.Message}").ConfigureAwait(false);
            return;
        }

        if (result.Success)
        {
            if (!string.IsNullOrEmpty(result.Output))
            {
                Console.Write(result.Output);
            }

            var nonErrors = result.Diagnostics
                .Where(d => d.Severity != CompilerDiagnosticSeverity.Error)
                .ToList();
            if (nonErrors.Count > 0)
            {
                CliHelpers.RenderDiagnostics(nonErrors, sourceText: null, Console.Error);
            }
        }
        else
        {
            CliHelpers.RenderDiagnostics(result.Diagnostics, sourceText: null, Console.Error);
        }
    }

    static bool LineStartsBlock(string line)
    {
        var hashIdx = line.IndexOf('#');
        var withoutComment = hashIdx >= 0 ? line.Substring(0, hashIdx) : line;
        var trimmed = withoutComment.TrimEnd();
        return trimmed.EndsWith(':');
    }
}
