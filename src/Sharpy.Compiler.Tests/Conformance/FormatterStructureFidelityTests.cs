using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Pretty;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Formatting preserves meaning, user-written-structure half (P22b, #2157 class): a fact the parser
/// records that is neither a comment nor an escape must still be written back. Cells found while
/// draining the escape matrix and by the emit-invariance sweep:
/// <list type="bullet">
/// <item><c>@[attr(...)]</c> — a .NET attribute — was written as the decorator <c>@attr(...)</c>, which
/// refuses with SPY0444 ("Unknown decorator"): the formatter turned a valid file into an invalid one.</item>
/// <item>An auto-property's explicit-interface qualifier (<c>property IFoo.x: int = 3</c>) was dropped,
/// turning an explicit interface implementation into a public property.</item>
/// <item>A union's methods (<c>UnionDef.Body</c>) were never written — the formatter deleted them, and
/// the comparer and normalizer ignored the body, so a round-trip oracle could not see it (found by the
/// P22b emit-invariance sweep).</item>
/// </list>
/// Each cell: <c>run(P)</c> succeeds with the pinned output, <c>Format(P)</c> is clean, re-parses to a
/// structurally equal AST, keeps the written spelling, runs identically and is idempotent.
/// </summary>
[Collection("HeavyCompilation")]
public class FormatterStructureFidelityTests : IntegrationTestBase
{
    public FormatterStructureFidelityTests(ITestOutputHelper output) : base(output) { }

    /// <summary>(label, program, expected stdout, spelling the formatted text must keep).</summary>
    public static TheoryData<string, string, string, string> Cells => new()
    {
        {
            "bracket_attribute",
            "@[obsolete(\"old\")]\ndef g() -> int:\n    return 1\n\ndef main():\n    print(2)\n",
            "2",
            "@[obsolete(\"old\")]"
        },
        {
            "explicit_interface_auto_property",
            "interface IFoo:\n    property x: int\n\nclass C(IFoo):\n    property IFoo.x: int = 3\n\ndef main():\n    i: IFoo = C()\n    print(i.x)\n",
            "3",
            "property IFoo.x: int = 3"
        },
        {
            // UnionDef.Body: the formatter used to delete every method of a union, so a __str__
            // override fell back to the default repr (`<__main__.Shape.Circle object>`).
            "union_methods",
            "union Shape:\n    case Circle(r: float)\n\n    def __str__(self) -> str:\n        return \"custom\"\n\ndef main():\n    s: Shape = Shape.Circle(1.0)\n    print(str(s))\n",
            "custom",
            "def __str__(self) -> str:"
        },
        {
            // TupleLiteral.IsListDisplay on a starred store target: written bare it re-parsed as a
            // tuple target (same binding, different AST — the comparer arm made the net refuse it).
            "list_display_star_target",
            "def main():\n    [b, *rest] = (4, 5, 6)\n    print(b, rest)\n",
            "4 [5, 6]",
            "[b, *rest] = (4, 5, 6)"
        },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    [Trait("Category", "Conformance")]
    public void Format_KeepsUserWrittenStructure_RunsIdentically(string label, string program, string expected, string kept)
    {
        var r0 = CompileAndExecute(program, executionTimeoutMs: 15_000);
        Assert.True(r0.Success, $"{label}: the original must run: " + string.Join("; ", r0.CompilationErrors) + r0.StandardError);
        Assert.Equal(expected, Normalize(r0.StandardOutput));

        var formatted = FormatterService.Format(program);
        Assert.True(formatted.Diagnostics.Count == 0, $"{label}: format reported diagnostics");
        var text = formatted.FormattedText;
        Output.WriteLine($"{label} formatted:\n{text}");
        Assert.Contains(kept, text);

        Assert.True(StructuralEqualityComparer.Instance.Equals(
            AstNormalizer.Instance.NormalizeModule(Parse(program, label)),
            AstNormalizer.Instance.NormalizeModule(Parse(text, label))),
            $"{label}: the formatted program must re-parse to the same AST:\n{text}");

        var r1 = CompileAndExecute(text, executionTimeoutMs: 15_000);
        Assert.True(r1.Success, $"{label}: the formatted program must run:\n{text}\n" + string.Join("; ", r1.CompilationErrors) + r1.StandardError);
        Assert.Equal(expected, Normalize(r1.StandardOutput));

        Assert.Equal(text, FormatterService.Format(text).FormattedText);
    }

    private static Sharpy.Compiler.Parser.Ast.Module Parse(string source, string label)
    {
        var lexer = new SLexer(source);
        var tokens = lexer.TokenizeAll();
        Assert.False(lexer.Diagnostics.HasErrors, $"{label}: lex failed:\n{source}");
        var parser = new SParser(tokens);
        var module = parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors, $"{label}: parse failed:\n{source}");
        return module;
    }

    private static string Normalize(string stdout) => stdout.Replace("\r\n", "\n").TrimEnd('\n');
}
