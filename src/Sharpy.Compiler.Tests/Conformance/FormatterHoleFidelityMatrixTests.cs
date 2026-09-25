using System.Collections.Immutable;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Formatting preserves meaning (#2024): a replacement field's source is carried byte-for-byte
/// lex → AST → unparse, so <c>format</c> never re-spells a hole. The unparser used to re-visit the
/// hole's expression, which changed a t-string's <c>Interpolation.expression</c>
/// (<c>' x'</c> → <c>'x'</c>, <c>"d['k']"</c> → <c>'d["k"]'</c>), collapsed a dict-headed hole into
/// the <c>{{</c> escape (unparseable) and a set-headed hole into SILENTLY WRONG valid syntax
/// (<c>f"{ {x, 2} }"</c> → <c>f"{{x, 2}}"</c> prints <c>{x, 2}</c>).
///
/// <para>Every cell program P is observed five ways: <c>r0 = run(P)</c>; <c>F = Format(P)</c> with
/// no diagnostics; F re-lexes and re-parses clean; <c>r1 = run(F)</c> and <c>r1 == r0</c>, and
/// <c>r0 ==</c> the pinned python oracle (3.12 for f-strings, 3.14 for t-string <c>repr</c>) wherever
/// python's output is deterministic — the set-headed cells pin <c>r0 == r1</c> only (python prints a
/// set in hash order, <c>{2, 5}</c>; Sharpy in insertion order, <c>{5, 2}</c>); and
/// <c>Format(F) == F</c>. Axes: prefix {f, t, df} × quoting {single, triple} × hole {leading/trailing
/// ws, tab, index with quotes, dict-headed, set-headed, nested spec <c>{ w }</c> (and a dict-headed
/// nested spec field), <c>!r</c> after ws, <c>=</c> with conversion and spec, brace inside a string,
/// nested f-string, same-quote string, newline after <c>{</c>/inside a binary/before <c>}</c>/after
/// <c>!r</c>/before <c>=</c>, <c>#</c> comment (single- and triple-quoted), <c>}</c> and <c>{</c> inside a
/// comment, <c>#</c> inside a nested string, backslash continuation, df dedent with a brace inside an
/// in-hole string or comment (the prescan follows the hole grammar), a statement's trailing comment
/// after a multi-line hole (#2022)}. The generated-AST cell covers the fallback path (an AST built
/// without source re-visits the expression and must pad a brace-headed hole on both the top-level and
/// nested-spec paths — python's <c>ast.unparse</c> rule).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class FormatterHoleFidelityMatrixTests : IntegrationTestBase
{
    public FormatterHoleFidelityMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Prelude = "def main():\n    x = 5\n    d = {'k': 7}\n    w = 6\n";

    /// <summary>(label, statement body lines, python oracle — null = r0 == r1 only).</summary>
    public static TheoryData<string, string, string?> Cells => new()
    {
        { "f.dict_headed", "print(f\"{ {'a': 1}['a'] }\")", "1" },
        { "f.set_headed", "print(f\"{ {x, 2} }\")", null },
        { "f.ws_both", "print(f\"{ x }\")", "5" },
        { "f.tab", "print(f\"{\tx\t}\")", "5" },
        { "f.index_quotes", "print(f\"{d['k']}\")", "7" },
        { "f.spec_nested_ws", "print(f\"{x:{ w }}|\")", "     5|" },
        { "f.spec_nested_dict_headed", "print(f\"{x:{ {'w': 6}['w'] }}|\")", "     5|" },
        { "f.conv_after_ws", "print(f\"{ x !r:>{ w }}|\")", "     5|" },
        { "f.selfdoc_conv_spec", "print(f\"{ x = !r:>4}|\")", " x =    5|" },
        { "f.brace_in_string", "print(f\"{'{'}{'}'}\")", "{}" },
        { "f.nested_fstring", "print(f\"{ f'{x}' }\")", "5" },
        { "f.same_quote_string", "print(f\"{\"a\"}\")", "a" },
        { "f.triple_dict_headed", "print(f\"\"\"{ {'a': 1}['a'] }\"\"\")", "1" },
        { "f.triple_set_headed", "print(f\"\"\"{ {x, 2} }\"\"\")", null },
        { "df.dict_headed", "print(df\"{ {'a': 1}['a'] }\")", "1" },
        { "df.triple_set_headed", "print(df\"\"\"\n        { {x, 2} }|\n        \"\"\")", null },
        { "t.ws_both", "print(repr(t\"{ x }\"))", "Template(strings=('', ''), interpolations=(Interpolation(5, ' x', None, ''),))" },
        { "t.index_quotes", "print(repr(t\"{d['k']}\"))", "Template(strings=('', ''), interpolations=(Interpolation(7, \"d['k']\", None, ''),))" },
        { "t.tab", "print(repr(t\"{\tx}\"))", "Template(strings=('', ''), interpolations=(Interpolation(5, '\\tx', None, ''),))" },
        { "t.dict_headed", "print(repr(t\"{ {'a': 1}['a'] }\"))", "Template(strings=('', ''), interpolations=(Interpolation(1, \" {'a': 1}['a']\", None, ''),))" },
        { "t.selfdoc_ws", "print(repr(t\"{ x = }\"))", "Template(strings=(' x = ', ''), interpolations=(Interpolation(5, ' x', 'r', ''),))" },
        { "t.spec_nested_ws", "print(repr(t\"{ x :{ w }}\"))", "Template(strings=('', ''), interpolations=(Interpolation(5, ' x', None, '6'),))" },
        { "t.triple_index_quotes", "print(repr(t\"\"\"{ d['k'] }\"\"\"))", "Template(strings=('', ''), interpolations=(Interpolation(7, \" d['k']\", None, ''),))" },
        // PEP 701 hole grammar (#2022): the unparser writes df/triple as a single-quoted f"...", so a
        // verbatim multi-line hole is legal output only because single-quoted holes may span lines.
        { "f.nl_around", "print(f\"\"\"{\n        x\n    }\"\"\")", "5" },
        { "f.nl_single_quoted", "print(f\"{x +\n        1}\")", "6" },
        { "f.comment", "print(f\"{x # c\n    }\")", "5" },
        { "f.brace_in_comment", "print(f\"\"\"{x # }\n    }\"\"\")", "5" },
        { "f.conv_then_newline", "print(f\"{x!r\n    }\")", "5" },
        { "f.conv_comment_spec", "print(f\"{x!r # c\n    :>4}|\")", "   5|" },
        { "f.selfdoc_comment", "print(f\"\"\"{x # c\n=}\"\"\")", "x \n=5" },
        { "f.continuation", "print(f\"{x \\\n+ 1}\")", "6" },
        { "f.selfdoc_newline_trailing_comment", "print(f\"{x\n=}\")  # note", "x\n=5" },
        { "t.nl_around", "print(repr(t\"\"\"{\nx\n}\"\"\"))", "Template(strings=('', ''), interpolations=(Interpolation(5, '\\nx', None, ''),))" },
        { "t.comment_excised", "print(repr(t\"\"\"{x # c1\n + 1 # c2\n}\"\"\"))", "Template(strings=('', ''), interpolations=(Interpolation(6, 'x \\n + 1', None, ''),))" },
        { "t.selfdoc_comment", "print(repr(t\"\"\"{x # c\n=}\"\"\"))", "Template(strings=('x \\n=', ''), interpolations=(Interpolation(5, 'x', 'r', ''),))" },
        { "df.dedent_brace_in_string", "print(df\"\"\"\n        a{'{'}b\n        \"\"\")", "a{b" },
        { "df.dedent_brace_in_comment", "print(df\"\"\"\n        a{x # {\n        }b\n        \"\"\")", "a5b" },
        { "f.hash_in_string", "print(f\"{ '#' # d\n    }\")", "#" },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    [Trait("Category", "Conformance")]
    public void Format_PreservesTheHole_RunsIdentically(string label, string statement, string? oracle)
    {
        var program = Prelude + "    " + statement + "\n";

        var r0 = CompileAndExecute(program, executionTimeoutMs: 15_000);
        Assert.True(r0.Success, $"{label}: the original must run: " + string.Join("; ", r0.CompilationErrors) + r0.StandardError);
        var out0 = Normalize(r0.StandardOutput);
        if (oracle != null)
            Assert.Equal(oracle, out0);

        var formatted = FormatterService.Format(program);
        Assert.True(formatted.Diagnostics.Count == 0, $"{label}: format reported diagnostics");
        var text = formatted.FormattedText;
        Output.WriteLine($"{label} formatted:\n{text}");
        AssertLexesAndParsesClean(label, text);

        var r1 = CompileAndExecute(text, executionTimeoutMs: 15_000);
        Assert.True(r1.Success, $"{label}: the formatted program must run:\n{text}\n" + string.Join("; ", r1.CompilationErrors) + r1.StandardError);
        Assert.Equal(out0, Normalize(r1.StandardOutput));

        Assert.Equal(text, FormatterService.Format(text).FormattedText);
    }

    /// <summary>
    /// An AST built without source (no <c>RawText</c>/<c>ExpressionText</c>/<c>SourceText</c>) takes the
    /// unparser's re-visit fallback: a brace-headed hole must be padded on the top-level path and on
    /// the nested-spec path, or <c>{{</c> is read back as an escape.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void GeneratedAst_BraceHeadedHole_IsPaddedOnBothPaths()
    {
        var module = Parse("v = f\"{ {x, 2} }|{x:{ {'w': 6}['w'] }}|{ {'a': 1}['a'] }\"\n");
        var literal = Assert.IsType<FStringLiteral>(Assert.IsType<Assignment>(module.Body[0]).Value);
        var sourceless = literal with { Parts = StripSource(literal.Parts) };

        var text = Pretty.Unparser.Unparse(sourceless);
        Output.WriteLine(text);
        AssertLexesAndParsesClean("generated", "v = " + text + "\n");

        var result = CompileAndExecute("def main():\n    x = 5\n    print(" + text + ")\n", executionTimeoutMs: 15_000);
        Assert.True(result.Success, "generated: must run:\n" + text + "\n" + string.Join("; ", result.CompilationErrors) + result.StandardError);
        Assert.Equal("{5, 2}|     5|1", Normalize(result.StandardOutput));
    }

    private static ImmutableArray<FStringPart> StripSource(ImmutableArray<FStringPart> parts) =>
        parts.Select(p => p with
        {
            RawText = null,
            ExpressionText = null,
            SourceText = null,
            Spec = p.Spec is { } spec ? StripSource(spec) : null,
        }).ToImmutableArray();

    private static Module Parse(string source)
    {
        var lexer = new SLexer(source);
        var tokens = lexer.TokenizeAll();
        Assert.False(lexer.Diagnostics.HasErrors, "must lex: " + source);
        return new SParser(tokens).ParseModule();
    }

    private static void AssertLexesAndParsesClean(string label, string text)
    {
        var lexer = new SLexer(text);
        var tokens = lexer.TokenizeAll();
        Assert.False(lexer.Diagnostics.HasErrors,
            $"{label}: re-lex failed:\n{text}\n" + string.Join("; ", lexer.Diagnostics.GetErrors()));
        var parser = new SParser(tokens);
        parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors,
            $"{label}: re-parse failed:\n{text}\n" + string.Join("; ", parser.Diagnostics.GetErrors()));
    }

    private static string Normalize(string stdout) => stdout.Replace("\r\n", "\n").TrimEnd('\n');
}
