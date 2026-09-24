using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// PEP 750 template surface, executed and compared byte-for-byte with python3.14.
///
/// <para><b>Expression text (#1991).</b> <c>Interpolation.expression</c> is the hole's source text —
/// from just after <c>{</c> to the top-level terminator (<c>}</c>, <c>=</c>, <c>!</c>, <c>:</c>),
/// leading whitespace kept, trailing whitespace stripped — captured by the lexer, never re-derived
/// from the AST. Axes: whitespace {leading, trailing, both, tab} × terminator {<c>}</c>, <c>=</c>
/// with/without spaces (+ the <c>=</c> form's conversion <c>'r'</c>/<c>None</c> twins), <c>!r</c>
/// after ws, <c>:</c> after ws, nested spec <c>{w}</c>} × expression shape {parenthesised, call with
/// args, index with quotes, brace inside a string literal, dict braces}. Each cell prints
/// <c>repr(tp)</c>, which spells every field (strings, value, expression, conversion, spec), so the
/// <c>=</c>-form strings and the evaluated nested spec are pinned on the same line. The oracle is the
/// identical program run by <c>/opt/homebrew/bin/python3.14</c> (2026-09-23); every expected line
/// below is its stdout.</para>
///
/// <para>A newline inside the hole (<c>t"""{\nx\n}"""</c> → python <c>'\nx'</c>) is not a cell: the
/// Sharpy lexer refuses a newline in a replacement field today (SPY0015, #2022).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class TemplateSurfaceMatrixTests : IntegrationTestBase
{
    public TemplateSurfaceMatrixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>(label, t-string source, python3.14 <c>repr()</c> of it).</summary>
    private static readonly (string Label, string TString, string Expected)[] ExpressionTextCells =
    {
        ("ws.leading", "t\"{ x}\"", "Template(strings=('', ''), interpolations=(Interpolation(1, ' x', None, ''),))"),
        ("ws.trailing", "t\"{x }\"", "Template(strings=('', ''), interpolations=(Interpolation(1, 'x', None, ''),))"),
        ("ws.both", "t\"{ x }\"", "Template(strings=('', ''), interpolations=(Interpolation(1, ' x', None, ''),))"),
        ("ws.tab", "t\"{\tx\t}\"", "Template(strings=('', ''), interpolations=(Interpolation(1, '\\tx', None, ''),))"),
        ("eq.nospace", "t\"{x=}\"", "Template(strings=('x=', ''), interpolations=(Interpolation(1, 'x', 'r', ''),))"),
        ("eq.spaces", "t\"{ x = }\"", "Template(strings=(' x = ', ''), interpolations=(Interpolation(1, ' x', 'r', ''),))"),
        ("eq.conv_r", "t\"{x = }\"", "Template(strings=('x = ', ''), interpolations=(Interpolation(1, 'x', 'r', ''),))"),
        ("eq.spec_conv_none", "t\"{x  =  :>5}\"", "Template(strings=('x  =  ', ''), interpolations=(Interpolation(1, 'x', None, '>5'),))"),
        ("bang.after_ws", "t\"{ x !r}\"", "Template(strings=('', ''), interpolations=(Interpolation(1, ' x', 'r', ''),))"),
        ("colon.after_ws", "t\"{ x :>5}\"", "Template(strings=('', ''), interpolations=(Interpolation(1, ' x', None, '>5'),))"),
        ("spec.nested", "t\"{x:{w}}\"", "Template(strings=('', ''), interpolations=(Interpolation(1, 'x', None, '5'),))"),
        ("shape.paren", "t\"{ (x + 1) }\"", "Template(strings=('', ''), interpolations=(Interpolation(2, ' (x + 1)', None, ''),))"),
        ("shape.call_args", "t\"{xs.pop(0)}\"", "Template(strings=('', ''), interpolations=(Interpolation(1, 'xs.pop(0)', None, ''),))"),
        ("shape.index_quotes", "t\"{d['k']}\"", "Template(strings=('', ''), interpolations=(Interpolation(7, \"d['k']\", None, ''),))"),
        ("shape.brace_in_string", "t\"{'{'}\"", "Template(strings=('', ''), interpolations=(Interpolation('{', \"'{'\", None, ''),))"),
        ("shape.dict_braces", "t\"{ {'a':1}['a'] }\"", "Template(strings=('', ''), interpolations=(Interpolation(1, \" {'a':1}['a']\", None, ''),))"),
    };

    [Fact]
    [Trait("Category", "Conformance")]
    public void InterpolationExpression_IsTheHoleSourceText_MatchesPython314()
    {
        Assert.Equal(ExpressionTextCells.Length, ExpressionTextCells.Select(c => c.Label).Distinct().Count());

        var body = string.Join("\n", ExpressionTextCells.Select(c => $"    print(repr({c.TString}))"));
        var source = "def main():\n    x = 1\n    w = 5\n    xs = [1, 2]\n    d = {'k': 7}\n" + body + "\n";

        var result = CompileAndExecute(source, executionTimeoutMs: 15_000);
        Assert.True(result.Success, "compile/run failed: " + string.Join("; ", result.CompilationErrors) + result.StandardError);

        var actual = result.StandardOutput.Replace("\r\n", "\n").Split('\n');
        var failures = new List<string>();
        for (int i = 0; i < ExpressionTextCells.Length; i++)
        {
            var (label, tstring, expected) = ExpressionTextCells[i];
            var line = i < actual.Length ? actual[i] : "<missing>";
            if (line != expected)
                failures.Add($"{label} {tstring}: expected {expected}, got {line}");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
