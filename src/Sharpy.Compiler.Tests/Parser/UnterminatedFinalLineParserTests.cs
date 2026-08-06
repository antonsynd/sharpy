using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Pretty;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using AstNs = Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// #1233: an indented <c>const</c> as the last line of a file with no trailing newline threw SPY0102
/// "Expected newline, got Dedent", and the AST came back null — so every LSP feature on the file went
/// dark at once. The cause was a statement production ending with <c>ExpectNewline()</c>, which
/// tolerates EOF but not <c>Dedent</c>, and it was never only <c>const</c>: fourteen productions
/// carried the same narrow contract.
///
/// <para>These are byte-exact sources. Every case asserts its own premise first — that the source
/// really does not end in a newline — because the whole defect is a missing final byte, and a
/// harness or editor that quietly appended one would leave a green test measuring nothing.</para>
///
/// <para>The direction of every change is error to parse, never parse to different-parse:
/// <c>ExpectStatementEnd</c> accepts a strict superset of <c>ExpectNewline</c>'s inputs, and the
/// extra inputs are exactly the ones the latter threw on. <see cref="TerminatedAndUnterminated"/>
/// pins that as an assertion rather than an argument — the same source with and without its final
/// newline must produce the same tree.</para>
/// </summary>
public class UnterminatedFinalLineParserTests
{
    private static ParserNs.Parser ParserFor(string source)
        => new(new LexerNs.Lexer(source).TokenizeAll());

    private static AstNs.Module ParseUnterminated(string source)
    {
        source.Should().NotEndWith("\n",
            "the case under test is a file with no final newline; a source that acquired one tests nothing");

        var parser = ParserFor(source);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "an unterminated final line must parse: "
            + string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message}")));
        return module;
    }

    /// <summary>
    /// One row per production converted from the mid-construct terminator to the statement-final one.
    /// Each ends with the production under test as the last line of an indented block, with no
    /// trailing newline — the exact shape that produced SPY0102.
    /// </summary>
    public static TheoryData<string, string> ConvertedProductions => new()
    {
        { "const (inferred type)", "def main() -> None:\n    const LIMIT = 42" },
        { "const (annotated)", "def main() -> None:\n    const LIMIT: int = 42" },
        { "const (class body)", "class C:\n    x: int = 1\n    const INNER = 7" },
        { "raise", "def main() -> None:\n    raise ValueError(\"x\")" },
        { "bare raise", "def main() -> None:\n    try:\n        pass\n    except ValueError:\n        raise" },
        { "assert", "def main() -> None:\n    assert True" },
        { "assert with message", "def main() -> None:\n    assert True, \"why\"" },
        { "import", "def main() -> None:\n    import os" },
        { "from import", "def main() -> None:\n    from os import path" },
        { "type alias", "class C:\n    x: int = 1\n    type Alias = int" },
        { "enum member", "enum Color:\n    RED = 1" },
        { "union case", "union Shape:\n    case Circle(radius: float)" },
        { "inline stub", "class C:\n    def m(self) -> int: ..." },
        { "body-less interface method", "interface I:\n    def m(self) -> int" },
        { "body-less interface property", "interface I:\n    property get p(self) -> int" },
        { "body-less interface event", "interface I:\n    event add e(self, h: Handler)" },
    };

    [Theory]
    [MemberData(nameof(ConvertedProductions))]
    public void ConvertedProduction_ParsesAsAnUnterminatedFinalLine(string label, string source)
    {
        _ = label;
        ParseUnterminated(source).Body.Should().NotBeEmpty();
    }

    /// <summary>
    /// The no-widening half. <c>ExpectStatementEnd</c> accepts more inputs than <c>ExpectNewline</c>,
    /// so the risk is not that something stops parsing but that something starts parsing
    /// <em>differently</em>. Adding the missing newline must therefore change nothing about the tree.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConvertedProductions))]
    public void TerminatedAndUnterminated_ProduceTheSameTree(string label, string source)
    {
        _ = label;
        var unterminated = ParseUnterminated(source);

        var withNewline = ParserFor(source + "\n");
        var terminated = withNewline.ParseModule();
        withNewline.Diagnostics.HasErrors.Should().BeFalse();

        StructuralEqualityComparer.Instance.Equals(
            AstNormalizer.Instance.NormalizeModule(unterminated),
            AstNormalizer.Instance.NormalizeModule(terminated))
            .Should().BeTrue("the final newline is not part of the program's meaning");
    }

    /// <summary>
    /// Productions that already used the statement-final contract. They were green before the batch
    /// and must stay green — the control that says the matrix above measures the conversions rather
    /// than some change in how the lexer ends a file.
    /// </summary>
    [Theory]
    [InlineData("annotated assignment", "def main() -> None:\n    x: int = 1")]
    [InlineData("plain assignment", "def main() -> None:\n    x: int = 1\n    x = 2")]
    [InlineData("pass", "def main() -> None:\n    pass")]
    [InlineData("return", "def main() -> int:\n    return 1")]
    [InlineData("auto-property", "class C:\n    property p: int = 0")]
    [InlineData("auto-event", "class C:\n    event e: Handler")]
    [InlineData("module-level const", "const TOP = 1")]
    public void AlreadyTerminalProduction_StaysGreen(string label, string source)
    {
        _ = label;
        ParseUnterminated(source).Body.Should().NotBeEmpty();
    }

    /// <summary>
    /// The other half of what the narrow contract broke, and the half that has nothing to do with
    /// trailing newlines: a block-consuming initializer (a <c>match</c> expression) swallows the
    /// statement's newline along with its own <c>Dedent</c>, so the statement is terminated by
    /// <c>Previous.Type == Dedent</c>. <c>ExpectNewline</c> rejects that at any position in the file.
    /// These sources are fully newline-terminated and still failed before the conversion.
    /// </summary>
    [Theory]
    [InlineData("const", "def main() -> None:\n    x: int = 1\n    const Y = match x:\n        case 1: 10\n        case _: 0\n    print(Y)\n")]
    [InlineData("assert", "def main() -> None:\n    x: int = 1\n    assert match x:\n        case 1: True\n        case _: False\n    print(x)\n")]
    [InlineData("raise", "def main() -> None:\n    x: int = 1\n    raise match x:\n        case _: ValueError(\"e\")\n")]
    public void BlockConsumingInitializer_TerminatesTheStatement(string label, string source)
    {
        _ = label;
        source.Should().EndWith("\n", "this case is about the post-Dedent terminator, not a missing newline");

        var parser = ParserFor(source);
        parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message}")));
    }

    /// <summary>
    /// CRLF changes nothing. The lexer normalizes line endings, so a CRLF file with no final
    /// terminator produces the identical <c>Dedent, Eof</c> tail an LF file does — it was equally
    /// red before the conversion and is equally green now. A lone trailing <c>\r</c> is a line
    /// terminator, so that row was always green and is included as the contrast.
    /// </summary>
    [Theory]
    [InlineData("CRLF, no final terminator", "def main() -> None:\r\n    const LIMIT = 42")]
    [InlineData("CRLF, trailing CR only", "def main() -> None:\r\n    const LIMIT = 42\r")]
    [InlineData("LF control", "def main() -> None:\n    const LIMIT = 42")]
    public void LineEndingStyle_DoesNotChangeTheOutcome(string label, string source)
    {
        _ = label;
        var parser = ParserFor(source);
        parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message}")));
    }

    /// <summary>
    /// The file-based fixture <c>terminators/const_at_eof_no_trailing_newline.spy</c> is committed
    /// without a final newline, and there is nothing in the toolchain that guarantees it stays that
    /// way — the repo has no <c>.gitattributes</c>, but <c>.editorconfig</c> sets
    /// <c>insert_final_newline = true</c> for every file, so any editor that honours it would append
    /// the byte and the fixture would go on passing while testing nothing.
    ///
    /// <para>This is that fixture's arrangement guard, asserted against the same path
    /// <c>FileBasedIntegrationTests</c> reads (fixture discovery resolves back into the source tree,
    /// so there is no copy to diverge from).</para>
    /// </summary>
    [Fact]
    public void NoTrailingNewlineFixture_StillHasNoTrailingNewline()
    {
        var path = Path.Combine(
            FixtureDiscoveryHelper.FixturesPath, "terminators", "const_at_eof_no_trailing_newline.spy");

        File.Exists(path).Should().BeTrue($"the fixture must exist at the discovered path {path}");

        var bytes = File.ReadAllBytes(path);
        bytes.Should().NotBeEmpty();
        bytes[^1].Should().NotBe((byte)'\n',
            "the fixture's whole point is a file that does not end in a newline; if an editor or hook "
            + "appended one, the fixture passes while exercising the terminated path everything else "
            + "already covers");
        bytes[^1].Should().NotBe((byte)'\r', "a lone trailing CR is a line terminator too");
    }

    /// <summary>
    /// The availability claim in the issue, at the parser level: the failure was not a diagnostic on
    /// one line but a null AST, which takes every downstream feature with it. The LSP half of this is
    /// in <c>Sharpy.Lsp.Tests</c>.
    /// </summary>
    [Fact]
    public void UnterminatedConst_YieldsAWholeTree_NotJustAnAbsentError()
    {
        var module = ParseUnterminated("def helper() -> int:\n    return 1\n\n\ndef main() -> None:\n    const LIMIT = 42");

        module.Body.Should().HaveCount(2, "both functions must survive, not merely the diagnostic disappear");
        module.Body[0].Should().BeOfType<AstNs.FunctionDef>().Which.Name.Should().Be("helper");
        module.Body[1].Should().BeOfType<AstNs.FunctionDef>().Which.Name.Should().Be("main");
    }
}
