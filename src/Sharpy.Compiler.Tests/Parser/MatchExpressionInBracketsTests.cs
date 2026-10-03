using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// #2210 (ruling R-DT) — a match expression directly inside brackets stays REFUSED, with a steer to
/// bind it to a local first. Matrix: <b>bracket kind {call, list, tuple, parenthesized operand,
/// subscript, dict} × {single-line attempt, multi-line}</b> = 12 cells, each asserting SPY0102 at
/// the <c>match</c> keyword with the steer; plus the controls that must NOT get the steer.
///
/// <para><b>Base classification (measured with <c>sharpyc run</c> at 6b208066e):</b> all 12 cells
/// were SPY0102 "Expected newline, got Case" at the first <c>case</c> token.</para>
/// </summary>
public class MatchExpressionInBracketsTests
{
    private const string Steer = "Bind it to a local first";

    private static readonly Dictionary<string, string> Brackets = new()
    {
        ["call"] = "print({0})",
        ["list"] = "v = [{0}]",
        ["tuple"] = "v = ({0}, 1)",
        ["paren"] = "v = ({0})",
        ["subscript"] = "v = d[{0}]",
        ["dict"] = "v = {{1: {0}}}",
    };

    private static readonly Dictionary<string, string> Forms = new()
    {
        ["single"] = "match x: case 1: \"one\" case _: \"other\"",
        ["multi"] = "match x:\n        case 1: \"one\"\n        case _: \"other\"",
    };

    public static IEnumerable<object[]> Cells() =>
        from b in Brackets.Keys from f in Forms.Keys select new object[] { b, f };

    [Theory]
    [MemberData(nameof(Cells))]
    public void MatchExpression_DirectlyInsideBrackets_IsRefused_WithTheBindToALocalSteer(string bracket, string form)
    {
        var statement = string.Format(System.Globalization.CultureInfo.InvariantCulture, Brackets[bracket], Forms[form]);
        var source = "def main():\n    x = 1\n    d = {\"one\": 1}\n    " + statement + "\n    print(1)\n";
        var matchColumn = 5 + statement.IndexOf("match", System.StringComparison.Ordinal);

        var errors = ParseErrors(source);

        errors.Should().NotBeEmpty(source);
        var first = errors[0];
        first.Code.Should().Be("SPY0102", source);
        first.Message.Should().Contain("A match expression cannot be written inside brackets").And.Contain(Steer);
        first.Line.Should().Be(4, source);
        first.Column.Should().Be(matchColumn, source);
    }

    /// <summary>Match expressions outside brackets keep parsing; none of them gets the steer.</summary>
    [Theory]
    [InlineData("def main():\n    x = 1\n    v = match x:\n        case 1: \"one\"\n        case _: \"other\"\n    print(v)\n")]
    [InlineData("def f(x: int) -> str:\n    return match x:\n        case 1: \"one\"\n        case _: \"other\"\n")]
    [InlineData("def main():\n    x = 1\n    v = \"n=\" + match x:\n        case 1: \"one\"\n        case _: \"other\"\n    print(v)\n")]
    [InlineData("def main():\n    x = 1\n    v = match x:\n        case 1: match x:\n            case 1: \"a\"\n            case _: \"b\"\n        case _: \"c\"\n    print(v)\n")]
    public void MatchExpression_OutsideBrackets_StillParses(string source)
    {
        ParseErrors(source).Should().BeEmpty(source);
    }

    /// <summary>
    /// A single-line match expression OUTSIDE brackets is a different refusal (no single-line arms);
    /// the bind-to-a-local steer would be wrong there, so it keeps the generic message.
    /// </summary>
    [Fact]
    public void SingleLineMatchExpression_OutsideBrackets_DoesNotGetTheSteer()
    {
        var errors = ParseErrors("def main():\n    x = 1\n    v = match x: case 1: \"one\" case _: \"other\"\n");
        errors.Should().NotBeEmpty();
        errors[0].Code.Should().Be("SPY0102");
        errors[0].Message.Should().Be("Expected newline, got Case");
    }

    private static List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> ParseErrors(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        parser.ParseModule();
        return lexer.Diagnostics.GetErrors().Concat(parser.Diagnostics.GetErrors()).ToList();
    }
}
