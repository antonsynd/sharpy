using Sharpy.Compiler.Formatting;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Formatting preserves meaning, comment half (#2077, #2068; the string half is
/// <see cref="FormatterStringFidelityMatrixTests"/>, the name half
/// <see cref="FormatterNameFidelityMatrixTests"/>). A comment has an anchor at every position —
/// statements, clause headers, decorators, non-statement body lines, block ends, end of file — and a
/// comment INSIDE a header's brackets makes the header verbatim (P22b Design Decisions 3 and 5). At
/// 4ef844961 (that commit's <c>sharpyc</c>) the EOF and arm cells DROPPED their comment — after
/// Phase 2 the SPY0912 net refuses them instead — and two MOVED it with the sequence intact:
/// <c>clause.else_header</c> (written on the <c>if</c> line) and <c>block_end.inner_indent</c>
/// (hoisted to the outer indent). Hence the exact-text assertion.
/// <para>Every cell program P: <c>r0 = run(P)</c> == the pinned output; <c>F = Format(P)</c> with
/// no diagnostics (a refusal is a red cell, never a pass); F re-lexes and re-parses clean; the
/// comment SEQUENCE of F equals P's and F is exactly the expected text (P itself for the cells
/// already in formatter layout; every cell's stdout is the same with the bug present, so these are
/// the discriminating observations); <c>run(F) == r0</c>; <c>Format(F) == F</c>.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class FormatterCommentFidelityMatrixTests : IntegrationTestBase
{
    public FormatterCommentFidelityMatrixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>(label, program already in formatter layout, pinned output).</summary>
    public static TheoryData<string, string, string> Cells => new()
    {
        { "clause.else_header", "def main():\n    x = 0\n    if x > 0:\n        print(\"pos\")\n    else:  # else header\n        print(\"nonpos\")\n", "nonpos" },
        { "block_end.inner_indent", "def main():\n    if True:\n        print(1)\n        # end of the if body\n    print(2)\n", "1\n2" },
        { "eof.with_newline", "def main():\n    print(1)\n\n\n# end of file\n", "1" },
        { "eof.without_newline", "def main():\n    print(1)\n\n\n# end of file", "1" },
        // A comment at the def's BODY indent at the end of the file ends the body: no blank lines.
        { "eof.body_indent", "def main():\n    print(1)\n    # end of the body\n", "1" },
        // Match-expression arms are non-statement body lines, like enum members (the blank line
        // after the match expression is the unparser's existing layout for it).
        { "match_expression.arm_comments", "def main():\n    x = 1\n    y = match x:  # the match\n        case 1: \"one\"  # the one arm\n        # before the rest\n        case _: \"other\"  # the rest\n        # end of the arms\n\n    print(y)\n", "one" },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    [Trait("Category", "Conformance")]
    public void Format_KeepsEveryComment_RunsIdentically(string label, string program, string pinned)
        => AssertFormatsTo(label, program, program.EndsWith('\n') ? program : program + "\n", pinned);

    /// <summary>
    /// A column-1 comment at the end of the file after a definition is module-level content: it is
    /// separated from the definition by the two blank lines any module-level item after a definition
    /// gets — inserted here, where the source has none (lead ruling, P22b Phase 4).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void Format_EofCommentAfterADef_GetsTheModuleLevelBlankLines()
        => AssertFormatsTo("eof.not_laid_out", "def main():\n    print(1)\n# end of file\n",
            "def main():\n    print(1)\n\n\n# end of file\n", "1");

    private void AssertFormatsTo(string label, string program, string expected, string pinned)
    {
        var r0 = CompileAndExecute(program, executionTimeoutMs: 15_000);
        Assert.True(r0.Success, $"{label}: the original must run: " + string.Join("; ", r0.CompilationErrors) + r0.StandardError);
        var out0 = Normalize(r0.StandardOutput);
        Assert.Equal(pinned, out0);

        var formatted = FormatterService.Format(program);
        Assert.True(formatted.Diagnostics.Count == 0,
            $"{label}: format reported " + string.Join("; ", formatted.Diagnostics.Select(d => $"{d.Code} {d.Message}")));
        var text = formatted.FormattedText;
        Output.WriteLine($"{label} formatted:\n{text}");

        var lexer = new SLexer(text);
        var tokens = lexer.TokenizeAll();
        Assert.False(lexer.Diagnostics.HasErrors, $"{label}: re-lex failed:\n{text}");
        var parser = new SParser(tokens);
        parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors, $"{label}: re-parse failed:\n{text}");

        Assert.Equal(FormatterTwins.Comments(program), FormatterTwins.Comments(text));

        // Placement: the sequence check above cannot see a comment MOVED in order — at 4ef844961 and
        // after Phase 2, `else:  # else header` was written on the `if` line and the block-end
        // comment was hoisted to the outer indent, both with the sequence intact.
        Assert.Equal(expected, text);

        var r1 = CompileAndExecute(text, executionTimeoutMs: 15_000);
        Assert.True(r1.Success, $"{label}: the formatted program must run:\n{text}\n" + string.Join("; ", r1.CompilationErrors) + r1.StandardError);
        Assert.Equal(out0, Normalize(r1.StandardOutput));

        Assert.Equal(text, FormatterService.Format(text).FormattedText);
    }

    private static string Normalize(string stdout) => stdout.Replace("\r\n", "\n").TrimEnd('\n');
}
