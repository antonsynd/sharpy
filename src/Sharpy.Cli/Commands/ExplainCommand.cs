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
            return HandleExplainCommand(code, list);
        });

        root.Subcommands.Add(command);
    }

    internal static int HandleExplainCommand(string? code, bool list, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        if (list)
        {
            var all = DiagnosticExplanations.GetAll();
            stdout.WriteLine(CliHelpers.CliBold("Documented Diagnostic Codes:"));
            stdout.WriteLine(CliHelpers.CliColor(new string('=', 60), "36"));

            string? lastCategory = null;
            foreach (var entry in all.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (entry.Value.Category != lastCategory)
                {
                    if (lastCategory != null)
                        stdout.WriteLine();
                    var catColor = CliHelpers.CategoryColor(entry.Value.Category);
                    stdout.WriteLine($"  {CliHelpers.CliColor($"[{entry.Value.Category}]", catColor, bold: true)}");
                    lastCategory = entry.Value.Category;
                }
                var entryColor = CliHelpers.CategoryColor(entry.Value.Category);
                stdout.WriteLine($"    {CliHelpers.CliColor(entry.Key, entryColor)}  {entry.Value.Title}");
            }

            stdout.WriteLine(CliHelpers.CliColor(new string('=', 60), "36"));
            stdout.WriteLine($"Total: {CliHelpers.CliBold(all.Count.ToString())} documented codes");
            return 0;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            stderr.WriteLine("Usage: sharpyc explain <code>");
            stderr.WriteLine("       sharpyc explain --list");
            stderr.WriteLine();
            stderr.WriteLine("Example: sharpyc explain SPY0200");
            return 1;
        }

        var trimmedCode = code!.Trim();
        var explanation = DiagnosticExplanations.Get(trimmedCode);
        if (explanation == null)
        {
            stderr.WriteLine($"No explanation found for diagnostic code '{trimmedCode}'.");
            stderr.WriteLine("Use 'sharpyc explain --list' to see all documented codes.");
            return 1;
        }

        var color = CliHelpers.CategoryColor(explanation.Category);
        stdout.WriteLine($"{CliHelpers.CliColor(explanation.Code, color, bold: true)}: {CliHelpers.CliBold(explanation.Title)}");
        stdout.WriteLine(CliHelpers.CliColor(new string('=', 60), "36"));
        stdout.WriteLine();
        stdout.WriteLine(explanation.Description);

        if (explanation.Example != null)
        {
            stdout.WriteLine();
            stdout.WriteLine(CliHelpers.CliColor("Example:", "36", bold: true));
            foreach (var line in explanation.Example.Split('\n'))
                stdout.WriteLine($"  {line}");
        }

        if (explanation.Fix != null)
        {
            stdout.WriteLine();
            stdout.WriteLine(CliHelpers.CliColor("Fix:", "36", bold: true));
            foreach (var line in explanation.Fix.Split('\n'))
                stdout.WriteLine($"  {line}");
        }

        stdout.WriteLine();
        stdout.WriteLine($"{CliHelpers.CliColor("Category:", "36", bold: true)} {CliHelpers.CliColor(explanation.Category, color, bold: true)}");
        return 0;
    }
}
