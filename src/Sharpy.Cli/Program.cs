using System.CommandLine;
using Sharpy.Cli.Commands;
using Sharpy.Compiler;

namespace Sharpy.Cli;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--version")
        {
            Console.WriteLine(VersionInfo.GetDetailedDisplayString());
            return 0;
        }

        // Everything after a bare `--` belongs to the program being run, not to sharpyc, so it is
        // split off before parsing and never reaches the option grammar (#1215).
        var (compilerArguments, programArguments) = CliHelpers.SplitAtDoubleDash(args);

        var rootCommand = new RootCommand("sharpyc - Sharpy Compiler");

        var globals = new GlobalOptions();
        globals.AddToCommand(rootCommand);

        BuildCommand.Configure(rootCommand, globals);
        CompileCommand.Configure(rootCommand, globals);
        var runCommand = RunCommand.Configure(rootCommand, globals, programArguments);
        ProjectCommand.Configure(rootCommand, globals);
        EmitCommand.Configure(rootCommand, globals);
        CacheCommand.Configure(rootCommand, globals);
        ExplainCommand.Configure(rootCommand, globals);
        LspCommand.Configure(rootCommand, globals);
        ReplCommand.Configure(rootCommand, globals);
        FormatCommand.Configure(rootCommand, globals);
        ServerCommand.Configure(rootCommand, globals);

        var parseResult = rootCommand.Parse(compilerArguments);
        if (!CliHelpers.ValidateProgramArgumentPlacement(parseResult, runCommand, programArguments))
        {
            return CliHelpers.ExitCompileError;
        }

        return parseResult.Invoke();
    }
}
