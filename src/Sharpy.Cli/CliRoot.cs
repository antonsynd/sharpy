using System.CommandLine;
using Sharpy.Cli.Commands;

namespace Sharpy.Cli;

/// <summary>
/// Builds the <c>sharpyc</c> root command: the global options, every command, and the
/// <c>--help</c> notes generated from <see cref="CommandOptionEffects"/>. <c>Program.Main</c> and the
/// test harness both build the root here, so the grammar the tests walk is the grammar users get.
/// </summary>
internal static class CliRoot
{
    /// <param name="programArguments">
    /// Tokens that followed a bare <c>--</c>, handed to <see cref="RunCommand.Configure"/> (#1215).
    /// </param>
    internal static (RootCommand Root, GlobalOptions Globals, Command Run) Build(IReadOnlyList<string>? programArguments = null)
    {
        var root = new RootCommand("sharpyc - Sharpy Compiler");
        var globals = new GlobalOptions();
        globals.AddToCommand(root);

        BuildCommand.Configure(root, globals);
        CompileCommand.Configure(root, globals);
        var run = RunCommand.Configure(root, globals, programArguments);
        ProjectCommand.Configure(root, globals);
        EmitCommand.Configure(root, globals);
        CacheCommand.Configure(root, globals);
        ExplainCommand.Configure(root, globals);
        LspCommand.Configure(root, globals);
        ReplCommand.Configure(root, globals);
        FormatCommand.Configure(root, globals);
        ServerCommand.Configure(root, globals);

        // Every command's non-honoured option cells, documented in --help from the one registry.
        CommandOptionEffects.Document(root);

        return (root, globals, run);
    }
}
