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

        // #1988 (R-BY): the operand kind is projected from the static type beyond the primitives.
        // python3: format([1, 2], '>10') -> TypeError: unsupported format string passed to list.__format__
        yield return new("list_no_format", "xs: list[int] = [1, 2]", "xs", ">10",
            StaticMessage: "unsupported format string passed to list.__format__");
        // python3: class C: pass; format(C(), '>10') -> TypeError: unsupported format string passed to C.__format__
        yield return new("user_class_no_format", "", "C()", ">10",
            StaticMessage: "unsupported format string passed to C.__format__");
        // A type implementing System.IFormattable owns its spec: never refused, rendered by the type.
        // python3 (class F: def __format__(self, s): return "F<" + s + ">"): format(F(), '>10') -> 'F<>10>';
        // format(F(), 'garbage') -> 'F<garbage>' — the positive control for the absence of SPY0609.
        yield return new("formattable_pad", "", "F()", ">10", Output: "F<>10>");
        yield return new("formattable_garbage", "", "F()", "garbage", Output: "F<garbage>");
        // A Sharpy-declared enum formats as its str (python's Enum.__format__ is str.__format__(str(self))).
        // python3: format(Color.RED, 'd') -> ValueError: Unknown format code 'd' for object of type 'str'
        yield return new("enum_d", "", "Color.RED", "d",
            StaticMessage: "Unknown format code 'd' for object of type 'str'");
        // python3: format(Color.RED, '>12') -> '   Color.RED'. Sharpy's str(Color.RED) is 'RED' (#2007);
        // what this cell pins is that the str kind accepts the spec and pads to the width.
        yield return new("enum_pad", "", "Color.RED", ">12", Output: "         RED");
        // complex has its own __format__ (Core's Complex kind, #2018).
        // python3: format(1+2j, 'd') -> ValueError: Unknown format code 'd' for object of type 'complex'
        yield return new("complex_d", "", "complex(1, 2)", "d",
            StaticMessage: "Unknown format code 'd' for object of type 'complex'");
    }

    // Module-level declarations the cells read: C has no __format__, F owns its spec through
    // System.IFormattable (the CLR spelling of __format__), Color is a Sharpy-declared enum.
    private const string Prelude =
        "from System import IFormattable, IFormatProvider\n\n\n"
        + "class C:\n    x: int = 1\n\n\n"
        + "class F(IFormattable):\n"
        + "    def to_string(self, fmt: str, provider: IFormatProvider) -> str:\n"
        + "        return \"F<\" + fmt + \">\"\n\n\n"
        + "enum Color:\n    RED = 1\n\n\n";

    /// <summary>Route label → the call expression for (value, spec) and whether it needs <c>import builtins</c>.</summary>
    private static readonly (string Route, Func<string, string, string> Expr, bool ImportsBuiltins)[] Routes =
    {
        ("fstring", (v, spec) => $"f\"[{{{v}:{spec}}}]\"", false),
        ("builtin", (v, spec) => $"\"[\" + format({v}, \"{spec}\") + \"]\"", false),
        ("builtin_qualified", (v, spec) => $"\"[\" + builtins.format({v}, \"{spec}\") + \"]\"", true),
        // Sharpy accepts the spec by keyword (python3's format() takes no keywords — a pre-existing
        // deviation); the keyword-bound literal is a binding the check must reach all the same.
        ("builtin_keyword", (v, spec) => $"\"[\" + format({v}, format_spec=\"{spec}\") + \"]\"", false),
        // Pipe-forward: `v |> format(spec)` binds as `format(v, spec)` — the piped value is the
        // value argument and the written literal the spec, so the check must bind the route's
        // effective positional list, not the call node's arguments.
        ("pipe", (v, spec) => $"\"[\" + ({v} |> format(\"{spec}\")) + \"]\"", false),
        // A literal str.format template: the hole's spec is paired with its positional operand.
        ("strformat", (v, spec) => $"\"[{{:{spec}}}]\".format({v})", false),
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
                    + Prelude
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

                    // A call route reports at the spec literal, str.format at the template literal (the
                    // f-string route at the hole's value).
                    if (route != "fstring")
                    {
                        var anchor = route == "strformat" ? $"\"[{{:{cell.Spec}}}]\"" : $"\"{cell.Spec}\"";
                        var anchorOffset = source.IndexOf(anchor, StringComparison.Ordinal);
                        if (spy0609[0].Span?.Start != anchorOffset)
                            failures.Add($"{label}: SPY0609 at offset {spy0609[0].Span?.Start}, expected the literal at {anchorOffset}");
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

    /// <summary>
    /// The str.format route pairs each field of a literal template with the operand it reads, the way
    /// Core's <c>Vformat</c> does (#1956): manual and auto numbering, a conversion (the spec then
    /// formats a <c>str</c>), and an auto field AFTER a nested-spec field (whose nested field claims an
    /// index). A field whose spec or operand is not static — a nested spec, an index access, a
    /// <c>str</c>-variable template — and a template Core rejects outright (mixed numbering) keep the
    /// runtime refusal; valid fields keep printing. Fields are walked in order and the walk ends at the
    /// first field the checker cannot decide (#1984): a later field's refusal never pre-empts an
    /// earlier field's runtime error.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void StrFormatTemplate_FieldsPairWithTheirOperands_1956()
    {
        const string eq = "'=' alignment not allowed in string format specifier";
        // python3 3.12 for each expression (s = 'ab'): the refusal or the rendering noted per row.
        var cells = new (string Label, string Expr, string? Static, string? Runtime, string? Output)[]
        {
            ("manual", "\"{0:=5}\".format(s)", eq, null, null),
            ("manual_second", "\"{1:=5}\".format(1, s)", eq, null, null),
            ("auto_second", "\"{}{:=5}\".format(1, s)", eq, null, null),
            ("conversion", "\"{!r:=8}\".format(5)", eq, null, null),
            ("after_nested", "\"{:{}}{:=5}\".format(1, \">3\", s)", eq, null, null),
            ("nested_dynamic", "\"{:{}}\".format(s, \"=5\")", null, eq, null),
            ("index_field", "\"{0[0]:=5}\".format(xs)", null, eq, null),
            ("str_variable_template", "t.format(s)", null, eq, null),
            ("mixed_numbering", "\"{}{0:=5}\".format(s)", null,
                "cannot switch from automatic field numbering to manual field specification", null),
            // #1984 (Decision 7): fields are walked in order and the walk stops at the first field the
            // checker cannot decide, because CPython raises the FIRST field's error.
            // python3: '{5}{0:=5}'.format('ab') -> IndexError: Replacement index 5 out of range for positional args tuple
            ("order_out_of_range_first", "\"{5}{0:=5}\".format(s)", null,
                "Replacement index 5 out of range for positional args tuple", null),
            // python3: '{0:=5}{5}'.format('ab') -> ValueError: '=' alignment ... (field 0 fails first)
            ("order_refused_first", "\"{0:=5}{5}\".format(s)", eq, null, null),
            // Only the FIRST refused field is reported: python3 '{0:=5}{1:+5}'.format('ab', 'ab') raises
            // on field 0 and never reaches field 1's sign refusal.
            ("order_first_refusal_only", "\"{0:=5}{1:+5}\".format(s, s)", eq, null, null),
            // A keyword field the call names is decided, so the walk continues to field 1 (SPY0234 is
            // reported for the keyword itself). python3: '{name}{0:=5}'.format('ab', name=1) -> ValueError: '=' ...
            ("order_keyword_matched", "\"{name}{0:=5}\".format(s, name=1)", eq, null, null),
            // A keyword field no argument names ends the walk (python3: KeyError: 'name'); Core's runtime
            // message differs from python's (#2008).
            ("order_keyword_unmatched", "\"{name}{0:=5}\".format(s)", null,
                "cannot use keyword arguments with format(), use format_map()", null),
            ("ok_manual", "\"{0:>5}\".format(s)", null, null, "   ab"),
            ("ok_conversion", "\"{!r:>6}\".format(s)", null, null, "  'ab'"),
            ("ok_nested", "\"{:{}}{:>3}\".format(1, \">3\", 2)", null, null, "  1  2"),
        };

        var failures = new List<string>();
        foreach (var (label, expr, staticMessage, runtime, output) in cells)
        {
            var source = "def main() -> None:\n    s: str = \"ab\"\n    t: str = \"{:=5}\"\n    xs: list[str] = [\"ab\"]\n"
                + "    print(" + expr + ")\n";
            var result = CompileAndExecute(source, executionTimeoutMs: 15_000);
            var spy0609 = result.RawDiagnostics
                .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification).ToList();

            if (staticMessage != null)
            {
                var templateOffset = source.IndexOf(expr.StartsWith('"') ? expr[..(expr.IndexOf("\".", StringComparison.Ordinal) + 1)] : expr,
                    StringComparison.Ordinal);
                if (spy0609.Count != 1 || spy0609[0].Message != staticMessage || spy0609[0].Span?.Start != templateOffset)
                    failures.Add($"{label}: expected one SPY0609 '{staticMessage}' at the template (offset {templateOffset}), got "
                        + string.Join("; ", result.RawDiagnostics.Select(d => $"{d.Code}@{d.Span?.Start}: {d.Message}")));
            }
            else if (spy0609.Count != 0)
            {
                failures.Add($"{label}: expected no SPY0609, got: {string.Join("; ", spy0609.Select(d => d.Message))}");
            }
            else if (runtime != null && !IsRuntimeRefusal(result, runtime))
            {
                failures.Add($"{label}: expected a runtime '{runtime}', got success={result.Success} "
                    + $"errors: {string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError.Trim()}");
            }
            else if (output != null && (!result.Success || result.StandardOutput.TrimEnd('\n', '\r') != output))
            {
                failures.Add($"{label}: expected '{output}', got success={result.Success} stdout='{result.StandardOutput}' "
                    + $"errors: {string.Join("; ", result.CompilationErrors)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// #1955 (R-BE): str.format takes positional fields only, so a keyword stays SPY0234 — and the
    /// message now carries the Core-declared steer to the two spellings that do take names. The
    /// positional and <c>format_map</c> spellings run; a callee that declares no steer gets none
    /// (the absence row's positive control is that its SPY0234 is present).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void StrFormatKeywords_CarryTheSteer_1955()
    {
        const string steer = "str.format takes positional fields only; use an f-string (f\"{...}\") or format_map({...})";
        var failures = new List<string>();

        foreach (var (label, expr) in new[]
        {
            ("named_field", "\"{name}\".format(name=1)"),
            ("nested_width", "\"{:{w}}\".format(1234, w=8)"),
        })
        {
            var result = CompileAndExecute("def main() -> None:\n    print(" + expr + ")\n", executionTimeoutMs: 15_000);
            var spy0234 = result.RawDiagnostics.Where(d => d.Code == DiagnosticCodes.Semantic.UnknownKeywordArgument).ToList();
            if (spy0234.Count != 1 || !spy0234[0].Message.EndsWith(steer, StringComparison.Ordinal))
                failures.Add($"{label}: expected one SPY0234 ending with the steer, got: "
                    + string.Join("; ", result.RawDiagnostics.Select(d => d.Code + ": " + d.Message)));
        }

        // python3: '{0}'.format(1) -> '1'; '{x}'.format_map({'x': 1}) -> '1'
        foreach (var (label, expr) in new[]
        {
            ("positional", "\"{0}\".format(1)"),
            ("format_map", "\"{x}\".format_map({\"x\": 1})"),
        })
        {
            var result = CompileAndExecute("def main() -> None:\n    print(" + expr + ")\n", executionTimeoutMs: 15_000);
            if (!result.Success || result.StandardOutput.TrimEnd('\n', '\r') != "1")
                failures.Add($"{label}: expected '1', got success={result.Success} stdout='{result.StandardOutput}' "
                    + $"errors: {string.Join("; ", result.CompilationErrors)}");
        }

        var unsteered = CompileAndExecute(
            "def f(a: int) -> int:\n    return a\n\n\ndef main() -> None:\n    print(f(b=1))\n", executionTimeoutMs: 15_000);
        var plain = unsteered.RawDiagnostics.Where(d => d.Code == DiagnosticCodes.Semantic.UnknownKeywordArgument).ToList();
        if (plain.Count == 0 || plain.Any(d => d.Message.Contains("format_map", StringComparison.Ordinal)))
            failures.Add("unsteered: expected a SPY0234 without the steer, got: "
                + string.Join("; ", unsteered.RawDiagnostics.Select(d => d.Code + ": " + d.Message)));

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
