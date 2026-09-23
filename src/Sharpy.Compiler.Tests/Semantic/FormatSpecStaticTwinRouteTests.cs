using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1956: a LITERAL format spec gets the static twin of Core's runtime refusal on every call route,
/// not only on the f-string route. Each cell is compiled through the f-string route (the reference
/// the static check was first built for) and through each call route; the routes must report the
/// SAME SPY0609 messages, at the spec literal, and every cell pins its expected outcome literally so
/// a regression on the reference route cannot make the comparison agree vacuously.
///
/// <para>Before the fix every refused call-route cell compiled and raised the ValueError/TypeError
/// only at runtime (measured by <c>run</c> at the pre-fix commit — see the commit body).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class FormatSpecStaticTwinRouteTests : IntegrationTestBase
{
    public FormatSpecStaticTwinRouteTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// One value and one literal spec. <c>StaticMessage</c> is CPython's wording when the spec is
    /// refusable at compile time (null otherwise); <c>Output</c> is the bracketed rendering when the
    /// program runs; <c>RuntimeMessage</c> is Core's refusal when the operand kind is not static.
    /// </summary>
    private sealed record Cell(
        string Label, string Decl, string Value, string Spec,
        string? StaticMessage = null, string? Output = null, string? RuntimeMessage = null);

    private static IEnumerable<Cell> Cells()
    {
        const string str = "s: str = \"ab\"";
        // python3: format('ab', '=5') -> ValueError: '=' alignment not allowed in string format specifier
        yield return new("str_eq", str, "s", "=5", StaticMessage: "'=' alignment not allowed in string format specifier");
        yield return new("str_sign", str, "s", "+5", StaticMessage: "Sign not allowed in string format specifier");
        yield return new("str_alt", str, "s", "#5", StaticMessage: "Alternate form (#) not allowed in string format specifier");
        yield return new("str_literal_operand", "", "\"ab\"", "=5", StaticMessage: "'=' alignment not allowed in string format specifier");
        // python3: format(1234567, ',b') -> ValueError: Cannot specify ',' with 'b'.
        yield return new("int_comma_b", "x: int = 1234567", "x", ",b", StaticMessage: "Cannot specify ',' with 'b'.");
        // python3: format(1.5, 'd') -> ValueError: Unknown format code 'd' for object of type 'float'
        yield return new("float_d", "y: float = 1.5", "y", "d", StaticMessage: "Unknown format code 'd' for object of type 'float'");
        // python3: format(None, 'd') -> TypeError: unsupported format string passed to NoneType.__format__
        yield return new("none_literal", "", "None", "d", StaticMessage: "unsupported format string passed to NoneType.__format__");

        // Inversion controls: a VALID literal spec keeps compiling and printing on every route.
        // python3: format(42, 'd') -> '42'; format('ab', '>5') -> '   ab'; format(3.14159, '.2f') -> '3.14'
        yield return new("int_d", "x: int = 42", "x", "d", Output: "42");
        yield return new("str_right", str, "s", ">5", Output: "   ab");
        yield return new("float_2f", "y: float = 3.14159", "y", ".2f", Output: "3.14");

        // An operand whose kind is not static is Core's to refuse at runtime, on every route alike.
        yield return new("unknown_operand", "o: object = \"ab\"", "o", "=5",
            RuntimeMessage: "'=' alignment not allowed in string format specifier");
    }

    /// <summary>Route label → the call expression for (value, spec) and whether it needs <c>import builtins</c>.</summary>
    private static readonly (string Route, Func<string, string, string> Expr, bool ImportsBuiltins)[] Routes =
    {
        ("fstring", (v, spec) => $"f\"[{{{v}:{spec}}}]\"", false),
        ("builtin", (v, spec) => $"\"[\" + format({v}, \"{spec}\") + \"]\"", false),
        ("builtin_qualified", (v, spec) => $"\"[\" + builtins.format({v}, \"{spec}\") + \"]\"", true),
        // Sharpy accepts the spec by keyword (python3's format() takes no keywords — a pre-existing
        // deviation); the keyword-bound literal is a binding the check must reach all the same.
        ("builtin_keyword", (v, spec) => $"\"[\" + format({v}, format_spec=\"{spec}\") + \"]\"", false),
    };

    [Fact]
    [Trait("Category", "Conformance")]
    public void LiteralSpec_EveryCallRouteAgreesWithTheFStringRoute_1956()
    {
        var cells = Cells().ToList();
        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        var failures = new List<string>();
        foreach (var cell in cells)
        {
            foreach (var (route, expr, importsBuiltins) in Routes)
            {
                var label = $"{cell.Label}/{route}";
                var source = (importsBuiltins ? "import builtins\n\n\n" : "")
                    + "def main() -> None:\n"
                    + (cell.Decl.Length > 0 ? "    " + cell.Decl + "\n" : "")
                    + "    print(" + expr(cell.Value, cell.Spec) + ")\n";
                var result = CompileAndExecute(source, executionTimeoutMs: 15_000);
                var spy0609 = result.RawDiagnostics
                    .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification)
                    .ToList();

                if (cell.StaticMessage is { } message)
                {
                    if (spy0609.Count != 1 || spy0609[0].Message != message)
                    {
                        failures.Add($"{label}: expected exactly one SPY0609 '{message}', got "
                            + (result.Success
                                ? $"a program that ran, printing '{result.StandardOutput.TrimEnd()}'"
                                : string.Join("; ", result.RawDiagnostics.Select(d => d.Code + ": " + d.Message))));
                        continue;
                    }

                    // A call route reports at the spec literal (the f-string route at the hole's value).
                    if (route != "fstring")
                    {
                        var specLiteralOffset = source.IndexOf($"\"{cell.Spec}\"", StringComparison.Ordinal);
                        if (spy0609[0].Span?.Start != specLiteralOffset)
                            failures.Add($"{label}: SPY0609 at offset {spy0609[0].Span?.Start}, expected the spec literal at {specLiteralOffset}");
                    }
                    continue;
                }

                if (spy0609.Count != 0)
                {
                    failures.Add($"{label}: expected no SPY0609 (inversion control), got: {string.Join("; ", spy0609.Select(d => d.Message))}");
                    continue;
                }

                if (cell.Output is { } output)
                {
                    if (!result.Success || result.StandardOutput.TrimEnd() != $"[{output}]")
                        failures.Add($"{label}: expected '[{output}]', got success={result.Success} stdout='{result.StandardOutput.TrimEnd()}' "
                            + $"errors: {string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError.Trim()}");
                }
                else if (cell.RuntimeMessage is { } runtime)
                {
                    if (!IsRuntimeRefusal(result, runtime))
                        failures.Add($"{label}: expected a runtime '{runtime}', got success={result.Success} "
                            + $"errors: {string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError.Trim()}");
                }
            }
        }

        Output.WriteLine($"Cells: {cells.Count} x {Routes.Length} routes. Failures: {failures.Count}");
        foreach (var f in failures)
            Output.WriteLine("  " + f);
        Assert.True(failures.Count == 0,
            $"{failures.Count} of {cells.Count * Routes.Length} route cells disagree:\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    /// <summary>
    /// The program compiled with no error diagnostic and then raised <paramref name="message"/> at
    /// runtime (the harness reports a runtime crash through <c>CompilationErrors</c> too, so
    /// "compiled" is read from the raw diagnostics).
    /// </summary>
    private static bool IsRuntimeRefusal(ExecutionResult result, string message)
        => !result.Success
            && !result.RawDiagnostics.Any(d => d.IsError)
            && (result.StandardError + "\n" + string.Join("\n", result.CompilationErrors))
                .Contains(message, StringComparison.Ordinal);

    /// <summary>
    /// The shapes the literal check deliberately does NOT reach keep Core's runtime refusal (the
    /// runtime twin's positive control), and a parenthesized literal is still a literal.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void NonLiteralSpecs_KeepTheRuntimeTwin_1956()
    {
        const string eq = "'=' alignment not allowed in string format specifier";
        var failures = new List<string>();

        foreach (var (label, body) in new[]
        {
            ("dynamic_positional", "    spec: str = \"=5\"\n    print(format(s, spec))\n"),
            ("dynamic_keyword", "    spec: str = \"=5\"\n    print(format(s, format_spec=spec))\n"),
        })
        {
            var result = CompileAndExecute("def main() -> None:\n    s: str = \"ab\"\n" + body, executionTimeoutMs: 15_000);
            if (!IsRuntimeRefusal(result, eq))
                failures.Add($"{label}: expected a runtime '{eq}', got success={result.Success} "
                    + $"errors: {string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError.Trim()}");
        }

        var parenthesized = CompileAndExecute(
            "def main() -> None:\n    s: str = \"ab\"\n    print(format(s, (\"=5\")))\n", executionTimeoutMs: 15_000);
        if (!parenthesized.RawDiagnostics.Any(d =>
                d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification && d.Message == eq))
            failures.Add("parenthesized_literal: expected SPY0609, got: "
                + string.Join("; ", parenthesized.RawDiagnostics.Select(d => d.Code + ": " + d.Message)));

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
