using System.CommandLine;
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

        var (rootCommand, _, runCommand) = CliRoot.Build(programArguments);

        var parseResult = rootCommand.Parse(compilerArguments);
        if (!CliHelpers.ValidateProgramArgumentPlacement(parseResult, runCommand, programArguments))
        {
            return CliHelpers.ExitCompileError;
        }

        return parseResult.Invoke();
    }
}
