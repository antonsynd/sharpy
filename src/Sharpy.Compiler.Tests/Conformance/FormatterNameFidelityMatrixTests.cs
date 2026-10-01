using Sharpy.Compiler.Formatting;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Formatting preserves meaning, name half (#2157; the string half is
/// <see cref="FormatterStringFidelityMatrixTests"/>). A backtick-escaped name must be written back
/// escaped at every position, or the formatted file stops parsing (an escaped KEYWORD) or silently
/// binds something else (an escaped member, which code generation reads verbatim while the bare
/// spelling is PascalCased by <c>NameCasing</c>). At 4ef844961 each cell below was damaged by
/// <c>format</c>, measured with that commit's <c>sharpyc</c>:
/// <list type="bullet">
/// <item><c>kwarg.keyword</c> — <c>f(`class`=7)</c> → <c>f(class=7)</c>: SPY0100, the file no longer parses.</item>
/// <item><c>except_as.keyword</c> — <c>except ValueError as `class`:</c> → <c>as class</c>: SPY0100.</item>
/// <item><c>import_as.keyword</c> / <c>from_import_as.keyword</c> — <c>import System as `def`</c>,
/// <c>from System import Math as `def`</c> → <c>as def</c>: SPY0101.</item>
/// <item><c>member.case_binding</c> — <c>c.`total`</c> → <c>c.total</c>, which binds the C# member
/// <c>Total</c>: the program printed <c>2</c> instead of <c>1</c>. SILENT WRONG (reach:program).</item>
/// </list>
/// Every cell program P: <c>r0 = run(P)</c> == the pinned output; <c>F = Format(P)</c> with no
/// diagnostics (the SPY0912 net would refuse a dropped escape); F re-lexes and re-parses clean; the
/// escaped-name multiset of F equals P's; <c>run(F) == r0</c>; <c>Format(F) == F</c>. Every position
/// the generator cannot reach (escaped KEYWORDS — T2 escapes identifiers only) is pinned by the
/// <c>Formatting/backtick_escapes</c> layout fixture.
/// </summary>
[Collection("HeavyCompilation")]
public class FormatterNameFidelityMatrixTests : IntegrationTestBase
{
    public FormatterNameFidelityMatrixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>(label, program, pinned output).</summary>
    public static TheoryData<string, string, string> Cells => new()
    {
        { "kwarg.keyword", "def f(`class`: int) -> int:\n    return `class` * 2\n\n\ndef main():\n    print(f(`class`=7))\n", "14" },
        { "except_as.keyword", "def main():\n    try:\n        raise ValueError(\"boom\")\n    except ValueError as `class`:\n        print(str(`class`))\n", "boom" },
        { "import_as.keyword", "import System as `def`\n\n\ndef main():\n    print(`def`.Math.Abs(-3))\n", "3" },
        { "from_import_as.keyword", "from System import Math as `def`\n\n\ndef main():\n    print(`def`.Abs(-3))\n", "3" },
        { "member.case_binding", "class C:\n    `total`: int = 1\n    Total: int = 2\n\n\ndef main():\n    c = C()\n    print(c.`total`)\n", "1" },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    [Trait("Category", "Conformance")]
    public void Format_KeepsEveryEscape_RunsIdentically(string label, string program, string pinned)
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

        Assert.Equal(FormatterTwins.EscapedNames(program), FormatterTwins.EscapedNames(text));

        var r1 = CompileAndExecute(text, executionTimeoutMs: 15_000);
        Assert.True(r1.Success, $"{label}: the formatted program must run:\n{text}\n" + string.Join("; ", r1.CompilationErrors) + r1.StandardError);
        Assert.Equal(out0, Normalize(r1.StandardOutput));

        Assert.Equal(text, FormatterService.Format(text).FormattedText);
    }

    private static string Normalize(string stdout) => stdout.Replace("\r\n", "\n").TrimEnd('\n');
}
