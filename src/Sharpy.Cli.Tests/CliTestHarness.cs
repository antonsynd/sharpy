using System.CommandLine;
using Sharpy.Cli;
using Sharpy.Cli.Commands;

namespace Sharpy.Cli.Tests;

/// <summary>
/// Test harness that reconstructs the CLI root command exactly as
/// <c>Program.Main</c> does, and provides helpers for parse-level assertions and
/// full invocation with captured console output. All command handlers return exit
/// codes rather than calling <see cref="Environment.Exit"/>, so every command is
/// safe to exercise via <see cref="Invoke"/>.
/// </summary>
internal static class CliTestHarness
{
    // Console.SetOut/SetError mutate process-global state. Only the capture helpers
    // here touch the console, so a single static lock fully serializes them across
    // xUnit's parallel test runners.
    private static readonly object ConsoleLock = new();

    /// <summary>
    /// Builds a fresh root command with all commands and global options configured,
    /// mirroring <c>Program.Main</c>.
    /// </summary>
    internal static (RootCommand Root, GlobalOptions Globals) BuildRoot()
    {
        var root = new RootCommand("sharpyc - Sharpy Compiler");
        var globals = new GlobalOptions();
        globals.AddToCommand(root);

        BuildCommand.Configure(root, globals);
        RunCommand.Configure(root, globals);
        ProjectCommand.Configure(root, globals);
        EmitCommand.Configure(root, globals);
        CacheCommand.Configure(root, globals);
        ExplainCommand.Configure(root, globals);
        LspCommand.Configure(root, globals);
        ReplCommand.Configure(root, globals);
        FormatCommand.Configure(root, globals);

        return (root, globals);
    }

    /// <summary>Parses a command line without invoking any action.</summary>
    internal static ParseResult Parse(string commandLine)
    {
        var (root, _) = BuildRoot();
        return root.Parse(commandLine);
    }

    /// <summary>
    /// Parses a command line and exposes the <see cref="GlobalOptions"/> instance so
    /// callers can read global option values via <c>GetValue</c>.
    /// </summary>
    internal static (ParseResult Result, GlobalOptions Globals) ParseWithGlobals(string commandLine)
    {
        var (root, globals) = BuildRoot();
        return (root.Parse(commandLine), globals);
    }

    /// <summary>
    /// Invokes a command line with stdout/stderr captured and returns the exit code.
    /// </summary>
    internal static CliInvocation Invoke(string commandLine)
    {
        lock (ConsoleLock)
        {
            var originalOut = Console.Out;
            var originalErr = Console.Error;
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            try
            {
                Console.SetOut(outWriter);
                Console.SetError(errWriter);

                var (root, _) = BuildRoot();
                var parseResult = root.Parse(commandLine);
#pragma warning disable CS0618 // ParseResult.Invoke is obsolete in System.CommandLine 2.x; matches production Program.cs usage
                var exitCode = parseResult.Invoke();
#pragma warning restore CS0618
                return new CliInvocation(exitCode, outWriter.ToString(), errWriter.ToString(), parseResult);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
            }
        }
    }
}

internal sealed record CliInvocation(int ExitCode, string StdOut, string StdErr, ParseResult ParseResult);
