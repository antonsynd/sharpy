using Sharpy.Compiler.Diagnostics;
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
/// <para><b>Typed surface (#1996).</b> A t-string and a <c>Template</c> annotation are the registry
/// symbol's CLR-backed type (<c>Sharpy.Template</c>), so members, <c>__iter__</c>, <c>+</c> and SPY0203
/// come from reflection — there is no bespoke semantic record. Axes: attribute {strings,
/// interpolations, values, value, expression, conversion, format_spec} × route {t-string literal,
/// <c>Template</c>-annotated parameter} probed by <c>b: bool = …</c> (SPY0220 naming the
/// CLR-derived type; an <c>Unknown</c> member would be silent and surface as SPY0908), <c>.bogus</c> ×
/// {Template, Interpolation} → SPY0203, iteration element type, <c>t"a" + t"b"</c> typed
/// <c>Template</c>, <c>t"a" + 1</c> refused (SPY0222), and the executed values against python3.14.</para>
///
/// <para><b>Hole grammar (#2022, PEP 701).</b> Inside a hole, whitespace may span lines (single- and
/// triple-quoted), a <c>#</c> comment runs to end of line and is excised from the expression text
/// (a <c>}</c> inside it closes nothing; a <c>#</c> inside a nested string is not a comment),
/// excision precedes the trailing-whitespace strip (<c>'x \n + 1'</c>), the <c>=</c> text keeps
/// newlines, whitespace/newline/comment may follow <c>!r</c>, a backslash continuation is kept
/// verbatim, and a form feed is whitespace. Oracle: python3.14 (2026-09-25).</para>
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
        ("nl.around", "t\"\"\"{\nx\n}\"\"\"", "Template(strings=('', ''), interpolations=(Interpolation(1, '\\nx', None, ''),))"),
        ("nl.single_quoted", "t\"{\nx}\"", "Template(strings=('', ''), interpolations=(Interpolation(1, '\\nx', None, ''),))"),
        ("nl.inside_binary", "t\"\"\"{x +\n1}\"\"\"", "Template(strings=('', ''), interpolations=(Interpolation(2, 'x +\\n1', None, ''),))"),
        ("nl.before_spec", "t\"{x\n:>4}\"", "Template(strings=('', ''), interpolations=(Interpolation(1, 'x', None, '>4'),))"),
        ("nl.before_eq", "t\"\"\"{x\n=}\"\"\"", "Template(strings=('x\\n=', ''), interpolations=(Interpolation(1, 'x', 'r', ''),))"),
        ("nl.before_bang", "t\"\"\"{x\n!r}\"\"\"", "Template(strings=('', ''), interpolations=(Interpolation(1, 'x', 'r', ''),))"),
        ("comment.excised", "t\"\"\"{x # c1\n + 1 # c2\n}\"\"\"", "Template(strings=('', ''), interpolations=(Interpolation(2, 'x \\n + 1', None, ''),))"),
        ("comment.brace_in_comment", "t\"\"\"{x # }\n}\"\"\"", "Template(strings=('', ''), interpolations=(Interpolation(1, 'x', None, ''),))"),
        ("comment.selfdoc", "t\"\"\"{x # c\n=}\"\"\"", "Template(strings=('x \\n=', ''), interpolations=(Interpolation(1, 'x', 'r', ''),))"),
        ("comment.after_eq", "t\"{x = # c\n}\"", "Template(strings=('x = \\n', ''), interpolations=(Interpolation(1, 'x', 'r', ''),))"),
        ("comment.after_conv", "t\"{x !r # c\n}\"", "Template(strings=('', ''), interpolations=(Interpolation(1, 'x', 'r', ''),))"),
        ("comment.hash_in_string", "t\"{ '#' # d\n}\"", "Template(strings=('', ''), interpolations=(Interpolation('#', \" '#'\", None, ''),))"),
        ("continuation", "t\"{x \\\n+ 1}\"", "Template(strings=('', ''), interpolations=(Interpolation(2, 'x \\\\\\n+ 1', None, ''),))"),
        ("ws.formfeed", "t\"{\fx\f}\"", "Template(strings=('', ''), interpolations=(Interpolation(1, '\\x0cx', None, ''),))"),
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

    private const string LiteralRoute = "def main():\n    x = 1\n    tp = t\"a{x}b\"\n";
    private const string AnnotationRoute = "def f(tp: Template):\n";

    /// <summary>(label, program, expected code, expected message substring). Each is the checker's
    /// opinion of a surface expression; before #1996 every member cell was silent (Unknown) and the
    /// C# compiler refused it (SPY0908), and iteration was SPY0320.</summary>
    private static readonly (string Label, string Source, string Code, string Message)[] TypeCells =
    {
        ("literal.strings", LiteralRoute + "    b: bool = tp.strings\n", DiagnosticCodes.Semantic.TypeMismatch, "'array[str]'"),
        ("literal.interpolations", LiteralRoute + "    b: bool = tp.interpolations\n", DiagnosticCodes.Semantic.TypeMismatch, "'array[Interpolation]'"),
        ("literal.values", LiteralRoute + "    b: bool = tp.values\n", DiagnosticCodes.Semantic.TypeMismatch, "'array[object]'"),
        ("literal.value", LiteralRoute + "    b: bool = tp.interpolations[0].value\n", DiagnosticCodes.Semantic.TypeMismatch, "'object'"),
        ("literal.expression", LiteralRoute + "    b: bool = tp.interpolations[0].expression\n", DiagnosticCodes.Semantic.TypeMismatch, "'str'"),
        ("literal.conversion", LiteralRoute + "    b: bool = tp.interpolations[0].conversion\n", DiagnosticCodes.Semantic.TypeMismatch, "'str | None'"),
        ("literal.format_spec", LiteralRoute + "    b: bool = tp.interpolations[0].format_spec\n", DiagnosticCodes.Semantic.TypeMismatch, "'str'"),
        ("literal.bogus", LiteralRoute + "    print(tp.bogus)\n", DiagnosticCodes.Semantic.UndefinedMember, "Type 'Template' has no member 'bogus'"),
        ("literal.interpolation_bogus", LiteralRoute + "    print(tp.interpolations[0].bogus)\n", DiagnosticCodes.Semantic.UndefinedMember, "Type 'Interpolation' has no member 'bogus'"),
        ("literal.iter_element", "def main():\n    x = 1\n    for part in t\"a{x}b\":\n        b: bool = part\n", DiagnosticCodes.Semantic.TypeMismatch, "'object'"),
        ("literal.self", LiteralRoute + "    b: bool = tp\n", DiagnosticCodes.Semantic.TypeMismatch, "'Template'"),
        ("add.template_template", "def main():\n    b: bool = t\"a\" + t\"b\"\n", DiagnosticCodes.Semantic.TypeMismatch, "'Template'"),
        ("add.template_int", "def main():\n    y = t\"a\" + 1\n", DiagnosticCodes.Semantic.InvalidBinaryOperation, "Type 'Template' does not support operator '+'"),
        ("annotation.self", AnnotationRoute + "    b: bool = tp\n", DiagnosticCodes.Semantic.TypeMismatch, "'Template'"),
        ("annotation.strings", AnnotationRoute + "    b: bool = tp.strings\n", DiagnosticCodes.Semantic.TypeMismatch, "'array[str]'"),
        ("annotation.interpolations", AnnotationRoute + "    b: bool = tp.interpolations\n", DiagnosticCodes.Semantic.TypeMismatch, "'array[Interpolation]'"),
        ("annotation.bogus", AnnotationRoute + "    print(tp.bogus)\n", DiagnosticCodes.Semantic.UndefinedMember, "Type 'Template' has no member 'bogus'"),
        ("annotation.iter_element", AnnotationRoute + "    for part in tp:\n        b: bool = part\n", DiagnosticCodes.Semantic.TypeMismatch, "'object'"),
    };

    [Fact]
    [Trait("Category", "Conformance")]
    public void TemplateSurface_IsTypedThroughTheClrTwin()
    {
        Assert.Equal(TypeCells.Length, TypeCells.Select(c => c.Label).Distinct().Count());

        var failures = new List<string>();
        foreach (var (label, source, code, message) in TypeCells)
        {
            var program = label.StartsWith("annotation.", StringComparison.Ordinal)
                ? source + "\ndef main():\n    f(t\"a\")\n"
                : source;
            var result = CompileAndExecute(program, executionTimeoutMs: 15_000);
            if (result.Success)
            {
                failures.Add($"{label}: expected {code} but it compiled and ran");
                continue;
            }
            var all = string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"));
            if (!result.RawDiagnostics.Any(d => d.Code == code && d.Message.Contains(message, StringComparison.Ordinal)))
                failures.Add($"{label}: expected {code} containing {message}, got {all}");
            if (result.RawDiagnostics.Any(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError))
                failures.Add($"{label}: SPY0908 reached — the checker had no opinion: {all}");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// Executed surface, pinned from <c>/opt/homebrew/bin/python3.14</c> running the identical program
    /// (minus the <c>Template</c> annotation). One line differs BY DESIGN (plan-bf0244 Decision 11,
    /// <c>template_strings.md</c>): <c>print(part)</c> of an <c>Interpolation</c> renders its value in
    /// Sharpy (<c>1</c>) where python prints the repr (<c>Interpolation(1, 'x', None, '')</c>).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void TemplateSurface_ExecutesLikePython314()
    {
        const string source = """
            def main():
                x = 1
                s = "ab"
                tp = t"a{x}b{s!r:>6}"
                print(len(tp.strings))
                print(tp.strings[0])
                print(tp.strings[2])
                for st in tp.strings:
                    print(repr(st))
                print(len(tp.interpolations))
                print(repr(tp.interpolations[1]))
                print(len(tp.values))
                print(tp.values[0])
                print(tp.values[1])
                i = tp.interpolations[1]
                print(i.value)
                print(i.expression)
                print(i.conversion)
                print(i.format_spec)
                print(tp.interpolations[0].conversion)
                for part in tp:
                    print(repr(part))
                print(list(tp))
                print(len(list(tp)))
                print(list(t""))
                both = t"a{x}" + t"b"
                print(repr(both))
                ann: Template = t"z{x}"
                print(repr(ann))
                for part in t"q{x}":
                    print(part)

            """;

        var expected = string.Join("\n", new[]
        {
            "3",
            "a",
            "",
            "'a'",
            "'b'",
            "''",
            "2",
            "Interpolation('ab', 's', 'r', '>6')",
            "2",
            "1",
            "ab",
            "ab",
            "s",
            "r",
            ">6",
            "None",
            "'a'",
            "Interpolation(1, 'x', None, '')",
            "'b'",
            "Interpolation('ab', 's', 'r', '>6')",
            "['a', Interpolation(1, 'x', None, ''), 'b', Interpolation('ab', 's', 'r', '>6')]",
            "4",
            "[]",
            "Template(strings=('a', 'b'), interpolations=(Interpolation(1, 'x', None, ''),))",
            "Template(strings=('z', ''), interpolations=(Interpolation(1, 'x', None, ''),))",
            "q",
            "1", // python3.14: Interpolation(1, 'x', None, '') — Sharpy renders (Decision 11)
        }) + "\n";

        var result = CompileAndExecute(source, executionTimeoutMs: 15_000);
        Assert.True(result.Success, "compile/run failed: " + string.Join("; ", result.CompilationErrors) + result.StandardError);
        Assert.Equal(expected, result.StandardOutput.Replace("\r\n", "\n"));
    }
}
