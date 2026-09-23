using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The ONE format engine seen from all FOUR of its user-facing consumers at once (#1883 and its
/// two siblings; the t-string joined with #1970). <c>format(v, spec)</c>, <c>"{:spec}".format(v)</c>,
/// <c>f"{v:spec}"</c> and <c>str(t"{v:spec}")</c> must print the same bytes as each other AND as
/// python3 3.12, for every cell below. The t-string consumer is Sharpy's RENDER of a template
/// (<c>Interpolation.ToString</c> applies the conversion, then the spec) agreeing with the f-string;
/// Python's own <c>str(Template)</c> does not render, so the oracle is the f-string's text (PEP 750:
/// the interpolation's conversion and format_spec are exactly the f-string's).
///
/// <para>Why three consumers and not one engine test: the engine was never the only renderer.
/// <c>str.format</c>'s spec-less <c>"{}"</c> hole appended the object to a StringBuilder (so
/// <c>"{}".format(100.0)</c> printed <c>100</c> and <c>"{}".format(None)</c> printed nothing at all)
/// and its <c>!s</c> conversion called <c>ToString()</c>, while the f-string's plain hole already
/// lowered to <c>Builtins.Str</c>. Three consumers, three answers for one value. A Core-only test
/// cannot see that, because the divergence lived in the plumbing on the way in.</para>
///
/// <para>Every ExpectedOutput is what python3 3.12 prints for the identical program; the generating
/// oracle is quoted next to each group. Each hole is wrapped in <c>[...]</c> so padding is
/// load-bearing and the comparison can be byte-exact.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class FormatEngineConsumerParityTests : IntegrationTestBase
{
    public FormatEngineConsumerParityTests(ITestOutputHelper output) : base(output) { }

    /// <summary>One value, one spec — rendered four ways, expected to agree with python3.</summary>
    private sealed record Cell(string Label, string Decl, string Value, string Spec, string Expected);

    // Literal: fstring, strformat, builtin, tstring. A consumer added to the program without
    // bumping this fails the line-count check.
    private const int Consumers = 4;

    [Fact]
    [Trait("Category", "Conformance")]
    public void FormatEngine_AllFourConsumersAgreeWithCPython()
    {
        var cells = Cells().ToList();
        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        // One program per cell would be 40 compilations; one program for all of them keeps the
        // suite fast and still pins every byte, because each line is labelled.
        var lines = new List<string>();
        var decls = new List<string>();
        var expected = new List<string>();

        foreach (var cell in cells)
        {
            if (cell.Decl.Length > 0 && !decls.Contains(cell.Decl))
                decls.Add(cell.Decl);

            var spec = cell.Spec.Length > 0 ? ":" + cell.Spec : "";
            lines.Add($"    print(\"{cell.Label}|fstring|[\" + f\"{{{cell.Value}{spec}}}\" + \"]\")");
            lines.Add($"    print(\"{cell.Label}|strformat|[\" + \"{{{spec}}}\".format({cell.Value}) + \"]\")");
            lines.Add($"    print(\"{cell.Label}|builtin|[\" + format({cell.Value}, \"{cell.Spec}\") + \"]\")");
            lines.Add($"    print(\"{cell.Label}|tstring|[\" + str(t\"{{{cell.Value}{spec}}}\") + \"]\")");

            expected.Add($"{cell.Label}|fstring|[{cell.Expected}]");
            expected.Add($"{cell.Label}|strformat|[{cell.Expected}]");
            expected.Add($"{cell.Label}|builtin|[{cell.Expected}]");
            expected.Add($"{cell.Label}|tstring|[{cell.Expected}]");
        }

        var source = "def main() -> None:\n"
            + string.Join("", decls.Select(d => "    " + d + "\n"))
            + string.Join("\n", lines) + "\n";

        var result = CompileAndExecute(source, executionTimeoutMs: 30_000);
        Assert.True(result.Success,
            "the parity program failed to compile or run: " + string.Join("; ", result.CompilationErrors)
            + " / " + result.StandardError);

        var actual = result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var failures = new List<string>();

        Assert.Equal(cells.Count * Consumers, expected.Count);
        Assert.Equal(expected.Count, actual.Length);
        for (int i = 0; i < expected.Count; i++)
        {
            if (actual[i] != expected[i])
                failures.Add($"expected '{expected[i]}', got '{actual[i]}'");
        }

        Output.WriteLine($"Consumer-parity cells: {cells.Count} x {Consumers} consumers = {expected.Count}. Failures: {failures.Count}");
        foreach (var f in failures)
            Output.WriteLine("  " + f);

        Assert.True(failures.Count == 0,
            $"{failures.Count} of {expected.Count} consumer renderings disagree with python3:\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    /// <summary>
    /// One conversion field — <c>{operand FORM}</c> where FORM is a conversion (<c>!r</c>, <c>!s</c>,
    /// <c>!a</c>), the self-documenting <c>=</c> (alone, with a spec, or with a conversion), or a
    /// conversion followed by a spec. <see cref="Observable"/> says whether dropping the conversion
    /// would change the bytes (python3: the rendering without the conversion differs from the
    /// rendering with it); a non-observable cell is a CONTROL — it pins agreement, not the
    /// conversion. The <c>=</c> prefix text is not the conversion and is present either way.
    /// </summary>
    private sealed record ConversionCell(string Operand, string Form, string Expected, bool Observable)
    {
        public string Label => "conv." + Operand + Form;

        // str.format has no '=' (it is an f-/t-string feature); it takes the conversion forms only.
        public bool HasStrFormatRoute => !Form.StartsWith("=", StringComparison.Ordinal);
    }

    // Literal rosters (the totality anchors), not derived from the cells.
    private static readonly string[] ConversionOperands = { "s", "xs", "n", "e", "b" };
    private static readonly string[] ConversionForms = { "!r", "!s", "!a", "=", "=:>6", "=!r", "!r:>6", "!s:>6", "!a:>6" };

    // Declarations for the operands: str "ab", list [1, 2], int 5, str "é" (built with chr so the
    // program source stays ASCII), bool True (str/repr spell True, format() spells 1 — the operand
    // that makes a conversion observable under a spec).
    private const string ConversionDecls =
        "    s: str = \"ab\"\n"
        + "    xs: list[int] = [1, 2]\n"
        + "    n: int = 5\n"
        + "    e: str = chr(233)\n"
        + "    b: bool = True\n";

    private static readonly (string Operand, string Form, string Reason)[] ConversionNotApplicable =
    {
        ("xs", "=:>6", "python3 raises TypeError: unsupported format string passed to list.__format__ "
            + "('=' with a spec and no conversion formats the list itself); Sharpy renders it (#1988)"),
    };

    /// <summary>
    /// #1970: the conversion (<c>!r</c>/<c>!s</c>/<c>!a</c>, and the implied <c>!r</c> of a bare
    /// <c>=</c>) and the <c>=</c> text reach the t-string render exactly as they reach the f-string —
    /// and the conversion forms through <c>str.format</c> too. Expected bytes are python3 3.12's
    /// f-string output; the t-string route is expected to print the same (PEP 750).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void FormatEngine_ConversionsAgreeAcrossRoutes()
    {
        var cells = ConversionCells().ToList();
        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        var lines = new List<string>();
        var expected = new List<string>();
        foreach (var cell in cells)
        {
            var field = "{" + cell.Operand + cell.Form + "}";
            lines.Add($"    print(\"{cell.Label}|fstring|[\" + f\"{field}\" + \"]\")");
            lines.Add($"    print(\"{cell.Label}|tstring|[\" + str(t\"{field}\") + \"]\")");
            expected.Add($"{cell.Label}|fstring|[{cell.Expected}]");
            expected.Add($"{cell.Label}|tstring|[{cell.Expected}]");
            if (cell.HasStrFormatRoute)
            {
                lines.Add($"    print(\"{cell.Label}|strformat|[\" + \"{{{cell.Form}}}\".format({cell.Operand}) + \"]\")");
                expected.Add($"{cell.Label}|strformat|[{cell.Expected}]");
            }
        }

        var source = "def main() -> None:\n" + ConversionDecls + string.Join("\n", lines) + "\n";
        var result = CompileAndExecute(source, executionTimeoutMs: 30_000);
        Assert.True(result.Success,
            "the conversion program failed to compile or run: " + string.Join("; ", result.CompilationErrors)
            + " / " + result.StandardError);

        var actual = result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Assert.Equal(expected.Count, actual.Length);
        var failures = new List<string>();
        for (int i = 0; i < expected.Count; i++)
        {
            if (actual[i] != expected[i])
                failures.Add($"expected '{expected[i]}', got '{actual[i]}'");
        }

        Output.WriteLine($"Conversion cells: {cells.Count} ({cells.Count(c => c.Observable)} observable, "
            + $"{cells.Count(c => !c.Observable)} controls), {expected.Count} renderings. Failures: {failures.Count}");
        foreach (var f in failures)
            Output.WriteLine("  " + f);

        Assert.True(failures.Count == 0,
            $"{failures.Count} of {expected.Count} conversion renderings disagree with python3:\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void FormatEngine_ConversionMatrix_IsTotal_AndEveryConversionIsObservable()
    {
        var executing = ConversionCells().Select(c => (c.Operand, c.Form)).ToHashSet();
        var na = ConversionNotApplicable.Select(r => (r.Operand, r.Form)).ToHashSet();
        var missing = ConversionOperands
            .SelectMany(o => ConversionForms.Select(f => (o, f)))
            .Where(p => !executing.Contains(p) && !na.Contains(p))
            .ToList();
        Assert.True(missing.Count == 0, "conversion matrix positions with no cell and no N/A row: "
            + string.Join(", ", missing.Select(p => p.o + p.f)));
        Assert.Empty(executing.Intersect(na));

        // 5 operands x 9 forms = 45 positions: 44 executing + 1 N/A (literal anchors).
        Assert.Equal(45, ConversionOperands.Length * ConversionForms.Length);
        Assert.Equal(44, executing.Count);
        Assert.Single(na);

        // Each conversion character must be OBSERVABLE in at least one cell, or dropping it would
        // pass vacuously: 'r' (!r, !r:>6, and the implied !r of a bare '=' / '=!r'), 's' (only under
        // a spec — format(x, "") is str(x) by definition, so bare !s is a control for every operand),
        // 'a'. '=:>6' carries no conversion (the '=' text only), so it is a control form.
        var observableForms = ConversionCells().Where(c => c.Observable).Select(c => c.Form).ToHashSet();
        foreach (var form in new[] { "!r", "!a", "=", "=!r", "!r:>6", "!s:>6", "!a:>6" })
            Assert.True(observableForms.Contains(form), $"no observable operand for conversion form '{form}'");
        Assert.DoesNotContain("!s", observableForms);
        Assert.DoesNotContain("=:>6", observableForms);
    }

    private static IEnumerable<ConversionCell> ConversionCells()
    {
        // python3 3.12:
        //   for name, v in [("s","ab"),("xs",[1,2]),("n",5),("e","\u00e9"),("b",True)]:
        //     for form in ["!r","!s","!a","=","=:>6","=!r","!r:>6","!s:>6","!a:>6"]:
        //       print(name, form, repr(eval('f"{' + name + form + '}"', {name: v})))
        // Observable = the f-string text differs from the same field with the conversion removed
        // (the '=' text kept; an implied !r counts as a conversion), as SHARPY renders it. The three
        // xs conversion-plus-spec cells would be observable in python3 (format([1, 2], ">6") raises
        // TypeError) but Sharpy's engine renders the unconverted list as its padded str (#1988), so
        // dropping the conversion is invisible there: they are controls.
        yield return new("s", "!r", "'ab'", true);
        yield return new("s", "!s", "ab", false);
        yield return new("s", "!a", "'ab'", true);
        yield return new("s", "=", "s='ab'", true);
        yield return new("s", "=:>6", "s=    ab", false);
        yield return new("s", "=!r", "s='ab'", true);
        yield return new("s", "!r:>6", "  'ab'", true);
        yield return new("s", "!s:>6", "    ab", false);
        yield return new("s", "!a:>6", "  'ab'", true);
        yield return new("xs", "!r", "[1, 2]", false);
        yield return new("xs", "!s", "[1, 2]", false);
        yield return new("xs", "!a", "[1, 2]", false);
        yield return new("xs", "=", "xs=[1, 2]", false);
        yield return new("xs", "=!r", "xs=[1, 2]", false);
        yield return new("xs", "!r:>6", "[1, 2]", false);
        yield return new("xs", "!s:>6", "[1, 2]", false);
        yield return new("xs", "!a:>6", "[1, 2]", false);
        yield return new("n", "!r", "5", false);
        yield return new("n", "!s", "5", false);
        yield return new("n", "!a", "5", false);
        yield return new("n", "=", "n=5", false);
        yield return new("n", "=:>6", "n=     5", false);
        yield return new("n", "=!r", "n=5", false);
        yield return new("n", "!r:>6", "     5", false);
        yield return new("n", "!s:>6", "     5", false);
        yield return new("n", "!a:>6", "     5", false);
        yield return new("e", "!r", "'\u00e9'", true);
        yield return new("e", "!s", "\u00e9", false);
        yield return new("e", "!a", "'\\xe9'", true);
        yield return new("e", "=", "e='\u00e9'", true);
        yield return new("e", "=:>6", "e=     \u00e9", false);
        yield return new("e", "=!r", "e='\u00e9'", true);
        yield return new("e", "!r:>6", "   '\u00e9'", true);
        yield return new("e", "!s:>6", "     \u00e9", false);
        yield return new("e", "!a:>6", "'\\xe9'", true);
        yield return new("b", "!r", "True", false);
        yield return new("b", "!s", "True", false);
        yield return new("b", "!a", "True", false);
        yield return new("b", "=", "b=True", false);
        yield return new("b", "=:>6", "b=     1", false);
        yield return new("b", "=!r", "b=True", false);
        yield return new("b", "!r:>6", "  True", true);
        yield return new("b", "!s:>6", "  True", true);
        yield return new("b", "!a:>6", "  True", true);
    }

    /// <summary>
    /// A spec CPython refuses. A STATIC spec is refused by name at compile time (SPY0609, CPython's
    /// wording); the same spec reached through a nested field is dynamic and refused by Core at
    /// runtime with the same wording and the same exception TYPE.
    /// </summary>
    private sealed record RefusalCell(string Label, string Decl, string Value, string Spec, string Message);

    [Fact]
    [Trait("Category", "Conformance")]
    public void FormatEngine_GroupingWithTheWrongType_IsRefusedStaticallyAndDynamically()
    {
        var cells = RefusalCells().ToList();
        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        var failures = new List<string>();

        foreach (var cell in cells)
        {
            // Static: the spec is literal text, so the compiler refuses it by name.
            var staticSource = "def main() -> None:\n"
                + "    " + cell.Decl + "\n"
                + $"    print(f\"{{{cell.Value}:{cell.Spec}}}\")\n";
            var staticResult = CompileAndExecute(staticSource, executionTimeoutMs: 15_000);

            if (staticResult.Success)
            {
                failures.Add($"{cell.Label}/static: expected SPY0609 but the program ran, printing '{staticResult.StandardOutput.TrimEnd()}'");
            }
            else
            {
                var spy0609 = staticResult.RawDiagnostics
                    .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification)
                    .ToList();
                if (spy0609.Count == 0)
                    failures.Add($"{cell.Label}/static: expected SPY0609, got: {string.Join("; ", staticResult.RawDiagnostics.Select(d => d.Code + ": " + d.Message))}");
                else if (!spy0609.Any(d => d.Message.Contains(cell.Message, StringComparison.Ordinal)))
                    failures.Add($"{cell.Label}/static: expected '{cell.Message}', got: {string.Join("; ", spy0609.Select(d => d.Message))}");
            }

            // Dynamic: the spec arrives through a nested replacement field, so the compiler cannot
            // see it and Core must raise the identical error at runtime.
            var dynamicSource = "def main() -> None:\n"
                + "    " + cell.Decl + "\n"
                + $"    spec: str = \"{cell.Spec}\"\n"
                + $"    print(f\"{{{cell.Value}:{{spec}}}}\")\n";
            var dynamicResult = CompileAndExecute(dynamicSource, executionTimeoutMs: 15_000);

            if (dynamicResult.Success)
            {
                failures.Add($"{cell.Label}/dynamic: expected a runtime refusal but the program printed '{dynamicResult.StandardOutput.TrimEnd()}'");
            }
            else
            {
                var haystack = dynamicResult.StandardError + "\n" + string.Join("\n", dynamicResult.CompilationErrors);
                if (!haystack.Contains(cell.Message, StringComparison.Ordinal))
                    failures.Add($"{cell.Label}/dynamic: expected '{cell.Message}', got stderr: {dynamicResult.StandardError.Trim()}");
            }
        }

        Output.WriteLine($"Refusal cells: {cells.Count} x 2 routes. Failures: {failures.Count}");
        foreach (var f in failures)
            Output.WriteLine("  " + f);

        Assert.True(failures.Count == 0,
            $"{failures.Count} of {cells.Count * 2} refusal routes disagree with python3:\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    /// <summary>
    /// One nested-spec program fragment for one consumer. <c>Expr</c> evaluates to a string that must
    /// equal <c>[Expected]</c> byte-for-byte (the brackets make padding load-bearing). The label
    /// encodes cell.consumer so a failure names both.
    /// </summary>
    private sealed record NestedRendering(string Label, string Decls, string Expr, string Expected);

    /// <summary>
    /// #1943: a replacement field's spec may itself contain replacement fields. The f-string route
    /// already split them (in the lexer); <c>str.format</c>/<c>format_map</c> split at the first
    /// <c>}</c> and raised <c>Unknown format code '{' for object of type 'int'</c>. This asserts the
    /// three (four, with <c>format_map</c>) consumers now agree with python3, one program per cell.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void FormatEngine_NestedSpecs_AgreeAcrossConsumers()
    {
        var rows = NestedRenderings().ToList();
        Assert.Equal(rows.Count, rows.Select(r => r.Label).Distinct().Count());

        var failures = new List<string>();
        foreach (var row in rows)
        {
            var source = "def main() -> None:\n"
                + string.Join("", row.Decls.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => "    " + d + "\n"))
                + $"    print(\"[\" + {row.Expr} + \"]\")\n";
            var result = CompileAndExecute(source, executionTimeoutMs: 15_000);
            if (!result.Success)
            {
                failures.Add($"{row.Label}: failed to compile/run: {string.Join("; ", result.CompilationErrors)} / {result.StandardError.Trim()}");
                continue;
            }
            var actual = result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n');
            if (actual != "[" + row.Expected + "]")
                failures.Add($"{row.Label}: expected '[{row.Expected}]', got '{actual}'");
        }

        Output.WriteLine($"Nested-spec renderings: {rows.Count}. Failures: {failures.Count}");
        foreach (var f in failures)
            Output.WriteLine("  " + f);

        Assert.True(failures.Count == 0,
            $"{failures.Count} of {rows.Count} nested-spec renderings disagree with python3:\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    private static IEnumerable<NestedRendering> NestedRenderings()
    {
        // nest.auto — the discriminating cell across all four consumers.
        // python3 -c "print(repr('{:{}}'.format(1234, '>8')))"  =>  '    1234'
        const string autoDecl = "v: int = 1234\nspec: str = \">8\"\nm: dict[str, object] = {\"a\": 1234, \"b\": \">8\"}";
        yield return new("nest.auto.fstring", autoDecl, "f\"{v:{spec}}\"", "    1234");
        yield return new("nest.auto.strformat", autoDecl, "\"{:{}}\".format(v, spec)", "    1234");
        yield return new("nest.auto.builtin", autoDecl, "format(v, spec)", "    1234");
        yield return new("nest.auto.format_map", autoDecl, "\"{a:{b}}\".format_map(m)", "    1234");

        // nest.manual — manual field numbering across both levels (str.format route).
        // python3 -c "print(repr('{0:{1}}'.format(1234, '>8')))"  =>  '    1234'
        yield return new("nest.manual", "", "\"{0:{1}}\".format(1234, \">8\")", "    1234");

        // nest.two_fields — two nested fields build one spec.
        // python3 -c "print(repr('{:{}{}}'.format(1234, '>', '8')))"  =>  '    1234'
        yield return new("nest.two_fields", "", "\"{:{}{}}\".format(1234, \">\", \"8\")", "    1234");

        // nest.conv — a conversion on the outer field, a nested field for the spec.
        // python3 -c "print(repr('{!r:{}}'.format('hi', '>8')))"  =>  "    'hi'"
        yield return new("nest.conv", "", "\"{!r:{}}\".format(\"hi\", \">8\")", "    'hi'");

        // nest.format_map — a second format_map cell with two string keys, one the spec.
        // python3 -c "print(repr('{a:{b}}'.format_map({'a': 7, 'b': '>4'})))"  =>  '   7'
        yield return new("nest.format_map", "mm: dict[str, object] = {\"a\": 7, \"b\": \">4\"}",
            "\"{a:{b}}\".format_map(mm)", "   7");

        // nest.spec_is_field_consumes_index — the nested field draws index 1 from the shared
        // auto-number stream (outer 0, nested 1, trailing 2).
        // python3 -c "print(repr('{:{}}{}'.format(1, '>3', 9)))"  =>  '  19'
        yield return new("nest.spec_is_field_consumes_index", "", "\"{:{}}{}\".format(1, \">3\", 9)", "  19");

        // escape.after_field — a closed field followed by an escaped '}}'; the control that the
        // brace scanner stops at the field's own closing brace.
        // python3 -c "print(repr('{:5}}}'.format(5)))"  =>  '    5}'
        yield return new("escape.after_field", "", "\"{:5}}}\".format(5)", "    5}");
    }

    /// <summary>
    /// #1943 nested-spec refusals that surface only through the runtime (<c>str.format</c>) route: a
    /// nested field two levels deep, and a nested field that resolves to a literal brace. CPython
    /// raises a <c>ValueError</c> with the exact wording asserted here; the f-string route allows one
    /// more level (depth 2), so these are per-route and str.format-only.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void FormatEngine_NestedSpecRefusals_MatchCPythonAtRuntime()
    {
        var cells = new (string Label, string Expr, string Message)[]
        {
            // python3 -c "'{:{:{}}}'.format(5, '>', 2)"  =>  ValueError: Max string recursion exceeded
            ("refuse.nest.depth2_strformat", "\"{:{:{}}}\".format(5, \">\", 2)", "Max string recursion exceeded"),
            // python3 -c "'{:{}}'.format(1, '{}')"
            //   =>  ValueError: Invalid format specifier '{}' for object of type 'int'
            ("refuse.nest.brace_spec", "\"{:{}}\".format(1, \"{}\")",
                "Invalid format specifier '{}' for object of type 'int'"),
        };

        var failures = new List<string>();
        foreach (var cell in cells)
        {
            var source = "def main() -> None:\n    print(" + cell.Expr + ")\n";
            var result = CompileAndExecute(source, executionTimeoutMs: 15_000);
            if (result.Success)
            {
                failures.Add($"{cell.Label}: expected a runtime ValueError but the program printed '{result.StandardOutput.TrimEnd()}'");
                continue;
            }
            var haystack = result.StandardError + "\n" + string.Join("\n", result.CompilationErrors);
            if (!haystack.Contains(cell.Message, StringComparison.Ordinal))
                failures.Add($"{cell.Label}: expected '{cell.Message}', got stderr: {result.StandardError.Trim()}");
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} of {cells.Length} nested-spec refusals disagree with python3:\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    /// <summary>
    /// A string-operand spec CPython refuses. The f-string route with a LITERAL spec is refused at
    /// compile time (SPY0609); <c>format(v, spec)</c> and <c>"{:spec}".format(v)</c> with a literal
    /// spec get no static diagnostic (the inertness trap — sibling #1956) and are refused by
    /// Core at runtime with the identical wording. <c>FStringOnly</c> marks the conversion cell,
    /// whose kind (<c>Str</c> via <c>!r</c>) only exists on the f-string route.
    /// </summary>
    private sealed record StrRefusalCell(string Label, string Decl, string Value, string Spec, string Message, bool FStringOnly = false);

    [Fact]
    [Trait("Category", "Conformance")]
    public void FormatEngine_StringOperandRefusals_FireOnEveryRoute()
    {
        var cells = StringOperandRefusalCells().ToList();
        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        var failures = new List<string>();
        foreach (var cell in cells)
        {
            var declLine = cell.Decl.Length > 0 ? "    " + cell.Decl + "\n" : "";

            // Static (f-string, literal spec) → SPY0609 with CPython's wording.
            var staticSource = "def main() -> None:\n" + declLine
                + $"    print(f\"{{{cell.Value}:{cell.Spec}}}\")\n";
            var staticResult = CompileAndExecute(staticSource, executionTimeoutMs: 15_000);
            if (staticResult.Success)
            {
                failures.Add($"{cell.Label}/static: expected SPY0609 but the program printed '{staticResult.StandardOutput.TrimEnd()}'");
            }
            else
            {
                var spy0609 = staticResult.RawDiagnostics
                    .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification).ToList();
                if (spy0609.Count == 0)
                    failures.Add($"{cell.Label}/static: expected SPY0609, got: {string.Join("; ", staticResult.RawDiagnostics.Select(d => d.Code + ": " + d.Message))}");
                else if (!spy0609.Any(d => d.Message.Contains(cell.Message, StringComparison.Ordinal)))
                    failures.Add($"{cell.Label}/static: expected '{cell.Message}', got: {string.Join("; ", spy0609.Select(d => d.Message))}");
            }

            if (cell.FStringOnly)
                continue;

            // The two RUNTIME arms: a literal spec through format() and through str.format gets no
            // static diagnostic, so Core must raise the identical ValueError at runtime.
            foreach (var (arm, expr) in new[]
            {
                ("builtin", $"format({cell.Value}, \"{cell.Spec}\")"),
                ("strformat", $"\"{{:{cell.Spec}}}\".format({cell.Value})"),
            })
            {
                var rtSource = "def main() -> None:\n" + declLine
                    + $"    print({expr})\n";
                var rtResult = CompileAndExecute(rtSource, executionTimeoutMs: 15_000);
                if (rtResult.Success)
                {
                    failures.Add($"{cell.Label}/{arm}: expected a runtime ValueError but printed '{rtResult.StandardOutput.TrimEnd()}'");
                    continue;
                }
                var haystack = rtResult.StandardError + "\n" + string.Join("\n", rtResult.CompilationErrors);
                if (!haystack.Contains(cell.Message, StringComparison.Ordinal))
                    failures.Add($"{cell.Label}/{arm}: expected '{cell.Message}', got stderr: {rtResult.StandardError.Trim()}");
            }
        }

        Output.WriteLine($"String-operand refusal cells: {cells.Count}. Failures: {failures.Count}");
        foreach (var f in failures)
            Output.WriteLine("  " + f);

        Assert.True(failures.Count == 0,
            $"{failures.Count} string-operand refusal routes disagree with python3:\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    private static IEnumerable<StrRefusalCell> StringOperandRefusalCells()
    {
        const string decl = "s: str = \"ab\"";
        // python3 -c "for spec in ['=5','0=5','x=5','+5','-5',' 5','#5']: ..."
        yield return new("refuse.str_eq", decl, "s", "=5", "'=' alignment not allowed in string format specifier");
        yield return new("refuse.str_zero_eq", decl, "s", "0=5", "'=' alignment not allowed in string format specifier");
        yield return new("refuse.str_fill_eq", decl, "s", "x=5", "'=' alignment not allowed in string format specifier");
        yield return new("refuse.str_sign_plus", decl, "s", "+5", "Sign not allowed in string format specifier");
        yield return new("refuse.str_sign_minus", decl, "s", "-5", "Sign not allowed in string format specifier");
        yield return new("refuse.str_sign_space", decl, "s", " 5", "Space not allowed in string format specifier");
        yield return new("refuse.str_alt", decl, "s", "#5", "Alternate form (#) not allowed in string format specifier");
        // Ordering: '=+5' parses '=' as align and '+' as a real sign, so the SIGN refusal wins;
        // '+=5' parses '+' as fill and '=' as align, so no sign is parsed and the '=' refusal wins.
        // (python3: format('ab','=+5') -> Sign not allowed; format('ab','+=5') -> '=' alignment.)
        yield return new("refuse.str_sign_before_eq", decl, "s", "=+5", "Sign not allowed in string format specifier");
        yield return new("refuse.str_fill_then_eq", decl, "s", "+=5", "'=' alignment not allowed in string format specifier");
        // The conversion cell: !r maps the operand kind to Str, so f"{5!r:=8}" is refused. This is
        // the f-string static route only (there is no format()/str.format literal conversion route).
        // python3 -c "'{!r:=8}'.format(5)"  =>  ValueError: '=' alignment not allowed in string format specifier
        yield return new("refuse.str_conv_eq", "", "5!r", "=8",
            "'=' alignment not allowed in string format specifier", FStringOnly: true);
    }

    private static IEnumerable<RefusalCell> RefusalCells()
    {
        // python3 -c "format(1234567, ',b')"  =>  ValueError: Cannot specify ',' with 'b'.
        yield return new("refuse.comma_b", "x: int = 1234567", "x", ",b", "Cannot specify ',' with 'b'.");
        yield return new("refuse.comma_o", "x: int = 1234567", "x", ",o", "Cannot specify ',' with 'o'.");
        yield return new("refuse.comma_x", "x: int = 1234567", "x", ",x", "Cannot specify ',' with 'x'.");
        yield return new("refuse.comma_X", "q: int = 255", "q", ",X", "Cannot specify ',' with 'X'.");
        // 'n' and 'c' take NEITHER separator.
        yield return new("refuse.under_n", "x: int = 1234567", "x", "_n", "Cannot specify '_' with 'n'.");
        yield return new("refuse.comma_n", "x: int = 1234567", "x", ",n", "Cannot specify ',' with 'n'.");
        yield return new("refuse.under_c", "c65: int = 65", "c65", "_c", "Cannot specify '_' with 'c'.");
        // The absent type stands for the operand's default, so a str reports 's'.
        yield return new("refuse.str_under", "s: str = \"abc\"", "s", "_", "Cannot specify '_' with 's'.");
        yield return new("refuse.str_comma", "s: str = \"abc\"", "s", ",", "Cannot specify ',' with 's'.");
        // A float takes '_b' past the separator check, so its OWN refusal is what surfaces —
        // the positive control that the separator check is not swallowing everything.
        yield return new("refuse.float_under_b", "y: float = 1.5", "y", "_b",
            "Unknown format code 'b' for object of type 'float'");
        yield return new("refuse.float_comma_b", "y: float = 1.5", "y", ",b", "Cannot specify ',' with 'b'.");
        yield return new("refuse.float_under_n", "y: float = 1.5", "y", "_n", "Cannot specify '_' with 'n'.");
        // #1944: a sign with the 'c' (character) presentation type is refused — statically (SPY0609
        // on the f-string hole) and dynamically (Core, through the nested-field route).
        yield return new("refuse.int_c_sign", "c65: int = 65", "c65", "+c",
            "Sign not allowed with integer format specifier 'c'");
    }

    /// <summary>
    /// #1944: the sign is ONE rule spanning every numeric presentation type. This anchor is a
    /// LITERAL roster of presentation types, not one derived from the cells, so a presentation type
    /// that grows a sign behavior without a parity row fails here. <c>%</c> is covered by the
    /// <c>sign.pct_*</c> cells, <c>c</c> by the <c>refuse.int_c_sign</c> refusal.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void FormatEngine_SignRule_CoversEveryPresentationType()
    {
        const string roster = "dnfFeEgGxXob%c";
        var signTypes = Cells()
            .Where(c => c.Label.StartsWith("sign.type_", StringComparison.Ordinal))
            .Select(c => c.Spec.TrimStart('+').Single())
            .ToHashSet();
        bool pctCovered = Cells().Any(c => c.Label.StartsWith("sign.pct_", StringComparison.Ordinal));
        bool cCovered = RefusalCells().Any(c => c.Label == "refuse.int_c_sign");

        var missing = roster.Where(t =>
            t == '%' ? !pctCovered
            : t == 'c' ? !cCovered
            : !signTypes.Contains(t)).ToList();

        Assert.True(missing.Count == 0,
            "presentation types in the sign roster with no parity cell: " + new string(missing.ToArray()));
    }

    private static IEnumerable<Cell> Cells()
    {
        // ---- #1883: an empty spec is str(value), identically in all three consumers -------------
        // python3 -c 'f=100.0
        // print(format(f,""), "{}".format(f), f"{f}")'   =>  100.0 100.0 100.0
        yield return new("empty.float_whole", "f: float = 100.0", "f", "", "100.0");
        yield return new("empty.float_frac", "g: float = 3.5", "g", "", "3.5");
        yield return new("empty.float_negzero", "nz: float = -0.0", "nz", "", "-0.0");
        yield return new("empty.float_big", "big: float = 1e20", "big", "", "1e+20");
        yield return new("empty.float_inf", "pinf: float = float(\"inf\")", "pinf", "", "inf");
        yield return new("empty.float_nan", "nnan: float = float(\"nan\")", "nnan", "", "nan");
        yield return new("empty.int", "i: int = 42", "i", "", "42");
        yield return new("empty.bool", "b: bool = True", "b", "", "True");
        yield return new("empty.str", "s: str = \"hi\"", "s", "", "hi");
        yield return new("empty.list_int", "lst: list[int] = [1, 2]", "lst", "", "[1, 2]");
        yield return new("empty.list_float", "flst: list[float] = [1.0, 2.5]", "flst", "", "[1.0, 2.5]");

        // A float with a non-empty spec but NO type code takes the same str() route.
        // python3 -c 'print(repr(format(100.0,">10")))'  =>  '     100.0'
        yield return new("notype.float_pad", "f: float = 100.0", "f", ">10", "     100.0");
        yield return new("notype.float_left", "f: float = 100.0", "f", "<8", "100.0   ");
        yield return new("notype.inf_pad", "pinf: float = float(\"inf\")", "pinf", ">10", "       inf");

        // ---- '_' groups every four digits under b/o/x/X ----------------------------------------
        // python3 -c 'x=1234567
        // print(format(x,"_b"), format(x,"_o"), format(x,"_x"), format(x,"#_x"))'
        yield return new("group.under_b", "x: int = 1234567", "x", "_b", "1_0010_1101_0110_1000_0111");
        yield return new("group.under_o", "x: int = 1234567", "x", "_o", "455_3207");
        yield return new("group.under_x", "x: int = 1234567", "x", "_x", "12_d687");
        yield return new("group.under_X", "x: int = 1234567", "x", "_X", "12_D687");
        yield return new("group.alt_under_x", "x: int = 1234567", "x", "#_x", "0x12_d687");
        yield return new("group.alt_under_b", "x: int = 1234567", "x", "#_b", "0b1_0010_1101_0110_1000_0111");
        yield return new("group.under_b_255", "q: int = 255", "q", "_b", "1111_1111");
        // Agreeing controls: the decimal presentations still group every three.
        yield return new("group.under_d", "x: int = 1234567", "x", "_d", "1_234_567");
        yield return new("group.under_bare", "x: int = 1234567", "x", "_", "1_234_567");
        yield return new("group.comma_d", "x: int = 1234567", "x", ",d", "1,234,567");
        yield return new("group.under_x_255", "q: int = 255", "q", "_x", "ff");

        // ---- the '0' fill and the separators interleave ----------------------------------------
        // python3 -c 'print(repr(format(74565,"#012_x")), repr(format(1234,"012_d")))'
        yield return new("zerofill.alt_x", "h: int = 74565", "h", "#012_x", "0x0_0001_2345");
        yield return new("zerofill.bin", "q: int = 255", "q", "020_b", "0_0000_0000_1111_1111");
        yield return new("zerofill.dec", "n: int = 1234", "n", "012_d", "0_000_001_234");
        yield return new("zerofill.comma", "n: int = 1234", "n", "012,d", "0,000,001,234");
        yield return new("zerofill.float", "d: float = 1234.5", "d", "015_.2f", "0_000_001_234.50");

        // ---- negatives are sign-magnitude, never two's complement -------------------------------
        // python3 -c 'print(format(-255,"x"), format(-1234567,"_x"))'  =>  -ff -12_d687
        yield return new("negative.hex", "m: int = -255", "m", "x", "-ff");
        yield return new("negative.bin", "m: int = -255", "m", "_b", "-1111_1111");
        yield return new("negative.alt_hex", "k: int = -1234567", "k", "#_x", "-0x12_d687");
        yield return new("negative.zerofill", "j: int = -1234", "j", "012,d", "-000,001,234");

        // ---- non-finite floats keep Python's spelling under a type code -------------------------
        // python3 -c 'print(format(float("inf"),"f"), format(float("inf"),"F"))'  =>  inf INF
        yield return new("nonfinite.inf_f", "pinf: float = float(\"inf\")", "pinf", "f", "inf");
        yield return new("nonfinite.inf_F", "pinf: float = float(\"inf\")", "pinf", "F", "INF");
        yield return new("nonfinite.nan_g", "nnan: float = float(\"nan\")", "nnan", "g", "nan");
        yield return new("nonfinite.inf_pct", "pinf: float = float(\"inf\")", "pinf", "%", "inf%");

        // ---- 'c' inside the range still renders ------------------------------------------------
        // python3 -c 'print(format(65,"c"))'  =>  A
        yield return new("codepoint.ok", "c65: int = 65", "c65", "c", "A");

        // ---- #1944: the sign applies to every numeric presentation type, including '%' ---------
        // python3 -c "print(format(1.5,'+%'), format(1.5,' %'), format(1.5,'-%'), format(1.5,'+.1%'))"
        yield return new("sign.pct_plus", "s15: float = 1.5", "s15", "+%", "+150.000000%");
        yield return new("sign.pct_space", "s15: float = 1.5", "s15", " %", " 150.000000%");
        yield return new("sign.pct_minus", "s15: float = 1.5", "s15", "-%", "150.000000%");
        yield return new("sign.pct_int_plus", "two: int = 2", "two", "+%", "+200.000000%");
        yield return new("sign.pct_bool_plus", "tt: bool = True", "tt", "+%", "+100.000000%");
        yield return new("sign.pct_prec_plus", "s15: float = 1.5", "s15", "+.1%", "+150.0%");
        // python3 -c "print(repr(format(float('inf'),'+%')))"  =>  '+inf%'
        yield return new("sign.pct_inf_plus", "pinf: float = float(\"inf\")", "pinf", "+%", "+inf%");
        // Every other presentation type takes the sign — value 42 (int); the type roster is
        // asserted complete by FormatEngine_SignRule_CoversEveryPresentationType.
        // python3 -c "print([format(42,'+'+t) for t in 'dnfFeEgGxXob'])"
        yield return new("sign.type_d", "i42: int = 42", "i42", "+d", "+42");
        yield return new("sign.type_n", "i42: int = 42", "i42", "+n", "+42");
        yield return new("sign.type_f", "i42: int = 42", "i42", "+f", "+42.000000");
        yield return new("sign.type_F", "i42: int = 42", "i42", "+F", "+42.000000");
        yield return new("sign.type_e", "i42: int = 42", "i42", "+e", "+4.200000e+01");
        yield return new("sign.type_E", "i42: int = 42", "i42", "+E", "+4.200000E+01");
        yield return new("sign.type_g", "i42: int = 42", "i42", "+g", "+42");
        yield return new("sign.type_G", "i42: int = 42", "i42", "+G", "+42");
        yield return new("sign.type_x", "i42: int = 42", "i42", "+x", "+2a");
        yield return new("sign.type_X", "i42: int = 42", "i42", "+X", "+2A");
        yield return new("sign.type_o", "i42: int = 42", "i42", "+o", "+52");
        yield return new("sign.type_b", "i42: int = 42", "i42", "+b", "+101010");

        // ---- #1945: the '0' flag zero-fills a STRING at its '<' default; explicit align overrides -
        // python3 -c "print(repr(format('ab','05')), repr(format('ab','>05')), repr(format('ab','<05')))"
        yield return new("zero.str_fill_default_left", "sab: str = \"ab\"", "sab", "05", "ab000");
        yield return new("zero.str_explicit_right", "sab: str = \"ab\"", "sab", ">05", "000ab");
        yield return new("zero.str_explicit_left", "sab: str = \"ab\"", "sab", "<05", "ab000");
        yield return new("zero.str_prec", "sab: str = \"ab\"", "sab", "05.1", "a0000");
        // python3 -c "print(repr(format('abcdef','.3')))"  =>  'abc'
        yield return new("prec.str", "sabc: str = \"abcdef\"", "sabc", ".3", "abc");
        // Numeric controls that must stay green: bool/int '0'-fill still =-synthesises.
        yield return new("zero.bool", "tt: bool = True", "tt", "05", "00001");

        // ---- #1958: '#' forces the decimal point and keeps trailing zeros on EVERY float
        //      presentation (e E f F g G n % and the type-less float), for int/bool operands that
        //      take a float presentation, whole-valued floats, '0'-fill and grouping. Controls that
        //      agreed before the fix: '#e' (always has a point), int '#n'/'#d' (integral), a
        //      fractional float with a bare '#', and non-finite (no point is ever added).
        // python3 -c "for v,s in [(42,'#g'),(42,'#G'),(42,'#e'),(42,'#.0f'),(42,'#.0F'),(42,'#.0%'),
        //   (42,'#012.3g'),(42,'#n'),(42,'#d'),(3.5,'#.0'),(3.5,'#.4g'),(3.5,'#5n'),(3.5,'#.6'),
        //   (3.5,'#.0E'),(3.5,'#.4e'),(3.5,'#'),(3.0,'#g'),(3.0,'#'),(3.0,'#.0f'),(3.0,'#n'),
        //   (0.0,'#.0e'),(True,'#g'),(True,'#d'),(1234.5,'#_.0f'),(float('inf'),'#g'),
        //   (float('inf'),'#.0f')]: print(repr(format(v,s)))"
        yield return new("alt.g_int", "i42: int = 42", "i42", "#g", "42.0000");
        yield return new("alt.G_int", "i42: int = 42", "i42", "#G", "42.0000");
        yield return new("alt.e_int", "i42: int = 42", "i42", "#e", "4.200000e+01");
        yield return new("alt.f0_int", "i42: int = 42", "i42", "#.0f", "42.");
        yield return new("alt.F0_int", "i42: int = 42", "i42", "#.0F", "42.");
        yield return new("alt.pct0_int", "i42: int = 42", "i42", "#.0%", "4200.%");
        yield return new("alt.zerofill_g", "i42: int = 42", "i42", "#012.3g", "0000000042.0");
        yield return new("alt.n_int", "i42: int = 42", "i42", "#n", "42");
        yield return new("alt.d_int", "i42: int = 42", "i42", "#d", "42");
        yield return new("alt.bare_prec0", "g: float = 3.5", "g", "#.0", "4.e+00");
        yield return new("alt.g4", "g: float = 3.5", "g", "#.4g", "3.500");
        yield return new("alt.n_width", "g: float = 3.5", "g", "#5n", "3.50000");
        yield return new("alt.bare_prec6", "g: float = 3.5", "g", "#.6", "3.50000");
        yield return new("alt.E0", "g: float = 3.5", "g", "#.0E", "4.E+00");
        yield return new("alt.e4", "g: float = 3.5", "g", "#.4e", "3.5000e+00");
        yield return new("alt.bare_float", "g: float = 3.5", "g", "#", "3.5");
        yield return new("alt.whole_g", "w3: float = 3.0", "w3", "#g", "3.00000");
        yield return new("alt.whole_bare", "w3: float = 3.0", "w3", "#", "3.0");
        yield return new("alt.whole_f0", "w3: float = 3.0", "w3", "#.0f", "3.");
        yield return new("alt.whole_n", "w3: float = 3.0", "w3", "#n", "3.00000");
        yield return new("alt.zero_e0", "z0: float = 0.0", "z0", "#.0e", "0.e+00");
        yield return new("alt.bool_g", "tt: bool = True", "tt", "#g", "1.00000");
        yield return new("alt.bool_d", "tt: bool = True", "tt", "#d", "1");
        yield return new("alt.group_f0", "d: float = 1234.5", "d", "#_.0f", "1_234.");
        yield return new("alt.inf_g", "pinf: float = float(\"inf\")", "pinf", "#g", "inf");
        yield return new("alt.inf_f0", "pinf: float = float(\"inf\")", "pinf", "#.0f", "inf");

        // ---- #1959: '=' pads AFTER the sign and the radix prefix on every fill path — explicit
        //      fill, the lone '=' (default space fill) and '0' fill (the control that agreed before
        //      the fix, because GroupAndZeroFill already skipped the prefix). 'd'/'f' and a
        //      prefix-less 'x' are sign-only controls.
        // python3 -c "for v,s in [(255,'*=#010x'),(-255,'=#10x'),(255,'0=#10x'),(255,'*=+#10x'),
        //   (-255,'*=#10x'),(-255,'*=#010x'),(255,'*=#10o'),(255,'*=#12b'),(-255,'*=#12_b'),
        //   (42,'*=-#012_X'),(255,'*=+8x'),(42,'*=+6d'),(3.5,'*=+8.1f')]: print(repr(format(v,s)))"
        yield return new("eqfill.star_hex", "q: int = 255", "q", "*=#010x", "0x******ff");
        yield return new("eqfill.lone_neg_hex", "m: int = -255", "m", "=#10x", "-0x     ff");
        yield return new("eqfill.zero_hex", "q: int = 255", "q", "0=#10x", "0x000000ff");
        yield return new("eqfill.plus_hex", "q: int = 255", "q", "*=+#10x", "+0x*****ff");
        yield return new("eqfill.neg_hex", "m: int = -255", "m", "*=#10x", "-0x*****ff");
        yield return new("eqfill.neg_hex_w010", "m: int = -255", "m", "*=#010x", "-0x*****ff");
        yield return new("eqfill.oct", "q: int = 255", "q", "*=#10o", "0o*****377");
        yield return new("eqfill.bin", "q: int = 255", "q", "*=#12b", "0b**11111111");
        yield return new("eqfill.neg_bin_group", "m: int = -255", "m", "*=#12_b", "-0b1111_1111");
        yield return new("eqfill.upper_hex", "i42: int = 42", "i42", "*=-#012_X", "0X********2A");
        yield return new("eqfill.nohash_hex", "q: int = 255", "q", "*=+8x", "+*****ff");
        yield return new("eqfill.d", "i42: int = 42", "i42", "*=+6d", "+***42");
        yield return new("eqfill.f", "g: float = 3.5", "g", "*=+8.1f", "+****3.5");
    }
}
