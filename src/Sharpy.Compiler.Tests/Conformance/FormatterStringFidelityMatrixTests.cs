using Sharpy.Compiler.Formatting;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;
using TriviaKind = Sharpy.Compiler.Lexer.TriviaKind;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Formatting preserves meaning, string-literal half (#2062, #2068; the replacement-field half is
/// <see cref="FormatterHoleFidelityMatrixTests"/>). The formatter must not rewrite a byte inside a
/// string literal or an f-/t-string, and must not drop or move a comment into one:
/// <list type="bullet">
/// <item>#2062 — <c>StripTrailingWhitespace</c> stripped every physical line, so a triple-quoted
/// string's inner line lost its trailing spaces (<c>"""p  \nq"""</c> printed <c>p</c> instead of
/// <c>p__</c>), a whitespace-only inner line was emptied, and a multi-line hole's trailing whitespace
/// (a t-string's <c>Interpolation.expression</c>) was cut. SILENT WRONG.</item>
/// <item>#2068 — a statement's trailing comment was inserted at the first newline of the statement's
/// output, INSIDE a multi-line string (<c>str("""e\nf""")  # note</c> printed <c>e  # note</c>), and a
/// comment after a statement that ends inside a multi-line string was dropped (the lexer classified
/// a comment as trailing only on the string's START line).</item>
/// </list>
/// Every cell program P: <c>r0 = run(P)</c> == python oracle (python3.14; the d-string cell pins its
/// PEP 822 dedent), <c>F = Format(P)</c> clean, F re-lexes/re-parses clean, <c>run(F) == r0</c>,
/// <c>Format(F) == F</c>, and the multiset of <c>#</c> comments in F equals P's. Axes: literal
/// {triple, raw triple, dedented, t-string multi-line hole} × damage {trailing spaces, trailing tab,
/// whitespace-only inner line} and trailing comment × {in a call, statement ends inside the string,
/// raw string}. The LSP twins live in <c>RangeFormattingTests</c>/<c>FormattingTests</c>.
/// </summary>
[Collection("HeavyCompilation")]
public class FormatterStringFidelityMatrixTests : IntegrationTestBase
{
    public FormatterStringFidelityMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Prelude = "def main():\n    x = 1\n";

    /// <summary>(label, statement lines, python oracle).</summary>
    public static TheoryData<string, string, string> Cells => new()
    {
        { "comment.after_string_in_call", "u = str(\"\"\"e\nf\"\"\")  # note\n    print(u)", "e\nf" },
        { "comment.statement_ends_inside_string", "v = \"\"\"g\nh\"\"\"  # note\n    print(v)", "g\nh" },
        { "comment.statement_ends_inside_raw_string", "w = r\"\"\"i\nj\"\"\"  # note\n    print(w)", "i\nj" },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    [Trait("Category", "Conformance")]
    public void Format_PreservesLiteralsAndComments_RunsIdentically(string label, string statements, string oracle)
    {
        var program = Prelude + "    " + statements + "\n";

        var r0 = CompileAndExecute(program, executionTimeoutMs: 15_000);
        Assert.True(r0.Success, $"{label}: the original must run: " + string.Join("; ", r0.CompilationErrors) + r0.StandardError);
        var out0 = Normalize(r0.StandardOutput);
        Assert.Equal(oracle, out0);

        var formatted = FormatterService.Format(program);
        Assert.True(formatted.Diagnostics.Count == 0, $"{label}: format reported diagnostics");
        var text = formatted.FormattedText;
        Output.WriteLine($"{label} formatted:\n{text}");

        var lexer = new SLexer(text);
        var tokens = lexer.TokenizeAll();
        Assert.False(lexer.Diagnostics.HasErrors, $"{label}: re-lex failed:\n{text}");
        var parser = new SParser(tokens);
        parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors, $"{label}: re-parse failed:\n{text}");

        Assert.Equal(Comments(program), Comments(text));

        var r1 = CompileAndExecute(text, executionTimeoutMs: 15_000);
        Assert.True(r1.Success, $"{label}: the formatted program must run:\n{text}\n" + string.Join("; ", r1.CompilationErrors) + r1.StandardError);
        Assert.Equal(out0, Normalize(r1.StandardOutput));

        Assert.Equal(text, FormatterService.Format(text).FormattedText);
    }

    /// <summary>The <c>#</c> comments of a source, sorted — the formatter may move a comment only along its own line.</summary>
    private static List<string> Comments(string source)
    {
        var tokens = new SLexer(source, preserveTrivia: true).TokenizeAll();
        return tokens
            .SelectMany(t => (t.LeadingTrivia ?? Array.Empty<Sharpy.Compiler.Lexer.Trivia>()).Concat(t.TrailingTrivia ?? Array.Empty<Sharpy.Compiler.Lexer.Trivia>()))
            .Where(t => t.Kind == TriviaKind.Comment)
            .Select(t => t.Text)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();
    }

    private static string Normalize(string stdout) => stdout.Replace("\r\n", "\n").TrimEnd('\n');
}
