using System.CommandLine;
using Sharpy.Compiler.Formatting;

namespace Sharpy.Cli.Commands;

internal static class FormatCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("format", "Format Sharpy source files");

        var inputArg = new Argument<string>("input") { Description = "Sharpy source file or directory to format" };
        var checkOpt = new Option<bool>("--check") { Description = "Exit with code 1 if any file would change (CI mode); do not modify files" };
        checkOpt.Aliases.Add("-c");
        var diffOpt = new Option<bool>("--diff") { Description = "Print a unified diff for each file that would change; do not modify files" };
        diffOpt.Aliases.Add("-d");
        var outputOpt = new Option<FileInfo?>("--output") { Description = "Write formatted output to the specified file (single-file input only)" };
        outputOpt.Aliases.Add("-o");
        var indentOpt = new Option<int?>("--indent") { Description = "Indent size in spaces (default: 4)" };
        var tabsOpt = new Option<bool>("--tabs") { Description = "Use tabs instead of spaces for indentation" };

        command.Arguments.Add(inputArg);
        command.Options.Add(checkOpt);
        command.Options.Add(diffOpt);
        command.Options.Add(outputOpt);
        command.Options.Add(indentOpt);
        command.Options.Add(tabsOpt);

        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var check = parseResult.GetValue(checkOpt);
            var diff = parseResult.GetValue(diffOpt);
            var output = parseResult.GetValue(outputOpt);
            var indent = parseResult.GetValue(indentOpt);
            var tabs = parseResult.GetValue(tabsOpt);
            return HandleFormatCommand(input, check, diff, output, indent, tabs);
        });

        root.Subcommands.Add(command);
    }

    static int HandleFormatCommand(
        string input,
        bool check,
        bool diff,
        FileInfo? output,
        int? indent,
        bool tabs)
    {
        if (check && diff)
        {
            Console.Error.WriteLine("Error: --check and --diff cannot be combined.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.Error.WriteLine("Error: input path is required.");
            return 2;
        }

        var isFile = File.Exists(input);
        var isDirectory = !isFile && Directory.Exists(input);
        if (!isFile && !isDirectory)
        {
            Console.Error.WriteLine($"Error: path '{input}' does not exist.");
            return 2;
        }

        if (output != null && isDirectory)
        {
            Console.Error.WriteLine("Error: --output is only supported when formatting a single file.");
            return 2;
        }

        var mode = check ? FormatMode.Check
            : diff ? FormatMode.Diff
            : FormatMode.Write;

        var formatOptions = FormatOptions.Default with
        {
            IndentSize = indent ?? FormatOptions.Default.IndentSize,
            UseTabs = tabs,
        };

        var runnerOptions = new FormatRunnerOptions
        {
            Mode = mode,
            FormatOptions = formatOptions,
            OutputPath = output?.FullName,
        };

        var result = FormatRunner.Run(input, runnerOptions);

        foreach (var outcome in result.Outcomes)
        {
            if (outcome.HasError)
            {
                Console.Error.WriteLine($"Error: {outcome.FilePath}: {outcome.ErrorMessage}");
                foreach (var diagnostic in outcome.Diagnostics.Where(d => d.IsError))
                {
                    Console.Error.WriteLine($"  {diagnostic.Code}: {diagnostic.Message}");
                }
                continue;
            }

            switch (mode)
            {
                case FormatMode.Check:
                    if (outcome.Changed)
                    {
                        Console.WriteLine($"Would reformat {outcome.FilePath}");
                    }
                    break;
                case FormatMode.Diff:
                    if (outcome.Changed && outcome.Diff != null)
                    {
                        Console.Write(outcome.Diff);
                    }
                    break;
                case FormatMode.Write:
                    if (outcome.Changed)
                    {
                        var destination = (output != null && isFile) ? output.FullName : outcome.FilePath;
                        Console.WriteLine($"Formatted {destination}");
                    }
                    break;
            }
        }

        switch (mode)
        {
            case FormatMode.Check:
                if (result.ChangedCount == 0 && result.ErrorCount == 0)
                {
                    Console.WriteLine("All files are already formatted.");
                }
                else if (result.ChangedCount > 0)
                {
                    Console.WriteLine($"{result.ChangedCount} file(s) would be reformatted.");
                }
                break;
            case FormatMode.Write:
                if (result.ErrorCount == 0)
                {
                    Console.WriteLine($"{result.ChangedCount} file(s) formatted.");
                }
                break;
        }

        return result.ExitCode;
    }
}
