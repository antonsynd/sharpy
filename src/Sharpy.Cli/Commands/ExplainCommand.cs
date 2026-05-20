using System.CommandLine;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Cli.Commands;

internal static class ExplainCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("explain", "Show detailed explanation for a diagnostic code");

        var codeArg = new Argument<string?>("code") { Description = "Diagnostic code to explain (e.g. SPY0200)", Arity = ArgumentArity.ZeroOrOne };
        var listOpt = new Option<bool>("--list") { Description = "List all documented diagnostic codes" };

        command.Arguments.Add(codeArg);
        command.Options.Add(listOpt);

        command.SetAction((parseResult) =>
        {
            var code = parseResult.GetValue(codeArg);
            var list = parseResult.GetValue(listOpt);
            HandleExplainCommand(code, list);
        });

        root.Subcommands.Add(command);
    }

    static void HandleExplainCommand(string? code, bool list)
    {
        if (list)
        {
            var all = DiagnosticExplanations.GetAll();
            Console.WriteLine(CliHelpers.CliBold("Documented Diagnostic Codes:"));
            Console.WriteLine(CliHelpers.CliColor(new string('=', 60), "36"));

            string? lastCategory = null;
            foreach (var entry in all.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (entry.Value.Category != lastCategory)
                {
                    if (lastCategory != null)
                        Console.WriteLine();
                    var catColor = CliHelpers.CategoryColor(entry.Value.Category);
                    Console.WriteLine($"  {CliHelpers.CliColor($"[{entry.Value.Category}]", catColor, bold: true)}");
                    lastCategory = entry.Value.Category;
                }
                var entryColor = CliHelpers.CategoryColor(entry.Value.Category);
                Console.WriteLine($"    {CliHelpers.CliColor(entry.Key, entryColor)}  {entry.Value.Title}");
            }

            Console.WriteLine(CliHelpers.CliColor(new string('=', 60), "36"));
            Console.WriteLine($"Total: {CliHelpers.CliBold(all.Count.ToString())} documented codes");
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            Console.Error.WriteLine("Usage: sharpyc explain <code>");
            Console.Error.WriteLine("       sharpyc explain --list");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Example: sharpyc explain SPY0200");
            Environment.Exit(1);
            return;
        }

        var trimmedCode = code!.Trim();
        var explanation = DiagnosticExplanations.Get(trimmedCode);
        if (explanation == null)
        {
            Console.Error.WriteLine($"No explanation found for diagnostic code '{trimmedCode}'.");
            Console.Error.WriteLine("Use 'sharpyc explain --list' to see all documented codes.");
            Environment.Exit(1);
            return;
        }

        var color = CliHelpers.CategoryColor(explanation.Category);
        Console.WriteLine($"{CliHelpers.CliColor(explanation.Code, color, bold: true)}: {CliHelpers.CliBold(explanation.Title)}");
        Console.WriteLine(CliHelpers.CliColor(new string('=', 60), "36"));
        Console.WriteLine();
        Console.WriteLine(explanation.Description);

        if (explanation.Example != null)
        {
            Console.WriteLine();
            Console.WriteLine(CliHelpers.CliColor("Example:", "36", bold: true));
            foreach (var line in explanation.Example.Split('\n'))
                Console.WriteLine($"  {line}");
        }

        if (explanation.Fix != null)
        {
            Console.WriteLine();
            Console.WriteLine(CliHelpers.CliColor("Fix:", "36", bold: true));
            foreach (var line in explanation.Fix.Split('\n'))
                Console.WriteLine($"  {line}");
        }

        Console.WriteLine();
        Console.WriteLine($"{CliHelpers.CliColor("Category:", "36", bold: true)} {CliHelpers.CliColor(explanation.Category, color, bold: true)}");
    }
}
