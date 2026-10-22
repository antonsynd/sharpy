using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

/// <summary>
/// <c>--max-errors N</c> reports at most N errors, plus the SPY0905 "further errors suppressed"
/// note where that phase emits one (#2311, ruling R-HD a). The parser used to read its cap at five
/// parallel panic-mode catch sites, each AFTER the recovery that had already reported a cascade:
/// a suite left at the cap reported its definition's <c>Expected Dedent</c> (SPY0104) as error
/// N+1. The cap is now enforced at the parser's one error-counting site, so no recovery follows
/// the Nth error.
/// </summary>
/// <remarks>
/// <para>
/// Closed-axis matrix: phase {parser, semantic (control)} × N {1, 2, default} × shape
/// {statement, def body, class body, nested suite} × route {<c>emit parse</c>,
/// <c>emit diagnostics</c>, <c>run</c>, <c>build</c>}. The parser phase has two more shapes,
/// enum body and union body, so every one of the five former catch sites (module, suite, enum,
/// union) is a row. Every program has N + 2 errors of its phase, so each row is one the cap must
/// cut. "default" is each phase's own default — 25 for the parser, 100 for semantic analysis.
/// <c>emit diagnostics</c> is the one route that prints warnings next to errors, so it alone also
/// pins the SPY0905 note. The semantic × <c>emit parse</c> cells do not exist (that route runs no
/// semantic phase) and are not generated. The LSP route has no cap option
/// (<c>CompilerOptionsFactory.ForLsp</c> is reached with <c>maxErrors</c> 0 only), so its
/// default-N rows live in <c>Sharpy.Lsp.Tests/MaxErrorsCapLspRouteTests</c>.
/// </para>
/// <para>
/// The parser rows of every shape but the statement are the ones the defect reached (N + 1 at
/// e7e0a0ef7); the statement rows, and every semantic row, are controls that already honoured the
/// cap.
/// </para>
/// <para>
/// <b>Every semantic phase (#2345).</b> The cap is ONE count at the project's diagnostic sink
/// (<c>DiagnosticBag.ErrorCap</c>), so the phase axis gains the phases that had no cap of their
/// own: type resolution (SPY0202 <c>x: Foo = 1</c>), name collection (SPY0204, a function or method
/// defined twice), import resolution (SPY0300 <c>import nomodN</c>), validation (SPY0400, a mutable
/// default) and a mixed program (SPY0202 and SPY0220 alternating — one count, not one per code).
/// At 4d2221242 every one reported every error under <c>--max-errors 1</c>. Shapes are those each
/// error can occupy; the default N is semantic analysis's 100.
/// </para>
/// </remarks>
public class MaxErrorsCapMatrixTests
{
    private const int ParserDefault = 25;
    private const int SemanticDefault = 100;

    private static readonly string[] Phases =
        { "parser", "semantic", "type-resolution", "name-collection", "import", "validation", "mixed" };
    private static readonly string[] SharedShapes = { "statement", "def", "class", "nested" };
    private static readonly string[] ParserOnlyShapes = { "enum", "union" };

    private static IEnumerable<string> ShapesOf(string phase) => phase switch
    {
        "parser" => SharedShapes.Concat(ParserOnlyShapes),
        "semantic" or "type-resolution" => SharedShapes,
        "name-collection" or "validation" => new[] { "statement", "class" },
        "import" or "mixed" => new[] { "statement" },
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };
    private static readonly int?[] Caps = { 1, 2, null };
    private static readonly string[] Routes = { "emit parse", "emit diagnostics", "run", "build" };

    public static IEnumerable<object[]> Cells()
    {
        foreach (var phase in Phases)
            foreach (var shape in ShapesOf(phase))
                foreach (var cap in Caps)
                    foreach (var route in Routes)
                    {
                        if (phase != "parser" && route == "emit parse")
                            continue; // `emit parse` runs no semantic phase.
                        yield return new object[] { phase, shape, cap?.ToString() ?? "default", route };
                    }
    }

    [Fact]
    public void Matrix_HasEveryCell()
    {
        // Anchored to literals, not to the axis arrays: parser 6 shapes × 3 N × 4 routes = 72; then
        // × 3 N × 3 routes (no `emit parse`): semantic 4 shapes = 36, type-resolution 4 = 36,
        // name-collection 2 = 18, import 1 = 9, validation 2 = 18, mixed 1 = 9 (#2345).
        Cells().Should().HaveCount(198);
        Cells().Select(c => $"{c[0]}|{c[1]}|{c[2]}|{c[3]}").Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void MaxErrors_ReportsAtMostNErrors(string phase, string shape, string cap, string route)
    {
        int? n = cap == "default" ? null : int.Parse(cap);
        var effective = n ?? (phase == "parser" ? ParserDefault : SemanticDefault);

        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(Program(phase, shape, effective + 2));
        var commandLine = new StringBuilder($"{route} \"{spy}\"");
        if (n is { } explicitCap)
            commandLine.Append($" --max-errors {explicitCap}");
        if (route == "build")
            commandLine.Append($" --output \"{ws.PathFor("out.dll")}\"");

        var invocation = CliTestHarness.Invoke(commandLine.ToString());
        var output = invocation.StdOut + invocation.StdErr;

        invocation.ExitCode.Should().NotBe(0, "the program has errors");
        var errorLine = route == "emit diagnostics"
            ? new Regex(@"^error SPY\d{4}\b", RegexOptions.Multiline)
            : new Regex(@"^error\[SPY\d{4}\]", RegexOptions.Multiline);
        var errors = errorLine.Matches(output).Count;
        errors.Should().Be(effective,
            $"`{commandLine}` must report exactly the cap's {effective} errors of the {effective + 2}+ "
            + $"the program has — never a cascade past the cap:\n{output}");

        if (route == "emit diagnostics")
        {
            Regex.Matches(output, @"^warning SPY0905\b", RegexOptions.Multiline).Count.Should().Be(1,
                $"the phase that stopped at the cap notes it exactly once:\n{output}");
        }
    }

    /// <summary>
    /// A program with at least <paramref name="count"/> errors of <paramref name="phase"/>, one in
    /// each of <paramref name="count"/> items of <paramref name="shape"/>, plus a <c>main</c> so no
    /// entry-point error joins them. (Uncapped, a union whose only case fails to parse also reports
    /// that it has no case.)
    /// </summary>
    private static string Program(string phase, string shape, int count)
    {
        var sb = new StringBuilder();
        for (var i = 1; i <= count; i++)
        {
            // Phases whose error is a whole declaration, not a statement.
            var declaration = (phase, shape) switch
            {
                ("name-collection", "statement") => $"def d{i}() -> None:\n    pass\n\ndef d{i}() -> None:\n    pass\n\n",
                ("name-collection", "class") => $"class C{i}:\n    def m(self) -> None:\n        pass\n\n    def m(self) -> None:\n        pass\n\n",
                ("validation", "statement") => $"def f{i}(x: list[int] = []) -> None:\n    pass\n\n",
                ("validation", "class") => $"class C{i}:\n    def m(self, x: list[int] = []) -> None:\n        pass\n\n",
                ("import", _) => $"import nomod{i}\n",
                _ => null,
            };
            if (declaration != null)
            {
                sb.Append(declaration);
                continue;
            }

            var bad = phase switch
            {
                "parser" => $"x{i} = = {i}",
                "type-resolution" => $"x{i}: Foo{i} = 1",
                "mixed" => i % 2 == 0 ? $"x{i}: int = \"s\"" : $"x{i}: Foo{i} = 1",
                _ => $"x{i}: int = undefined_{i}",
            };
            sb.Append(shape switch
            {
                "statement" => $"{bad}\n",
                "def" => $"def f{i}() -> None:\n    {bad}\n\n",
                "class" => $"class C{i}:\n    {bad}\n\n",
                "nested" => $"def f{i}() -> None:\n    if True:\n        {bad}\n\n",
                "enum" => $"enum E{i}:\n    A = = {i}\n\n",
                "union" => $"union U{i}:\n    case A(x: = int)\n\n",
                _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
            });
        }
        sb.Append("\ndef main() -> None:\n    pass\n");
        return sb.ToString();
    }
}
