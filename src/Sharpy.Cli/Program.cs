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

        var rootCommand = new RootCommand("sharpyc - Sharpy Compiler");

        var globals = new GlobalOptions();
        globals.AddToCommand(rootCommand);

        BuildCommand.Configure(rootCommand, globals);
        CompileCommand.Configure(rootCommand, globals);
        RunCommand.Configure(rootCommand, globals);
        ProjectCommand.Configure(rootCommand, globals);
        EmitCommand.Configure(rootCommand, globals);
        CacheCommand.Configure(rootCommand, globals);
        ExplainCommand.Configure(rootCommand, globals);
        LspCommand.Configure(rootCommand, globals);
        ReplCommand.Configure(rootCommand, globals);
        FormatCommand.Configure(rootCommand, globals);

        return rootCommand.Parse(args).Invoke();
    }
}
