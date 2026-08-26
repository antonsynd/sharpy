using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// PEP 634 group vs sequence patterns (#1624): a parenthesized pattern with NO comma is the inner
/// pattern (a group); a trailing comma or two or more elements make a tuple (sequence) pattern.
/// CPython (3.12, <c>ast.parse</c>): <c>case (y):</c> → <c>MatchAs</c>; <c>case (y,):</c> →
/// <c>MatchSequence</c>. Before the fix every parenthesized pattern was a <c>TuplePattern</c>, so
/// <c>case (y):</c> over an <c>int</c> was refused as "Cannot destructure non-tuple type" and the
/// irrefutable-arm ordering rule could not see the capture.
///
/// <para>Mutation record (commit body): reverting the group-pattern arm in
/// <c>Parser.ParseTuplePattern</c> turns the three group cases red (they observe a
/// <c>TuplePattern</c> of one element) while the sequence cases stay green.</para>
/// </summary>
public class GroupPatternParsingTests
{
    private static Pattern FirstCasePattern(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(string.Join("; ", parser.Diagnostics.GetErrors().Select(d => d.Message)));
        var match = module.Body.OfType<MatchStatement>().Single();
        return match.Cases[0].Pattern;
    }

    [Fact]
    public void ParenthesizedCapture_IsTheCapture()
    {
        var pattern = FirstCasePattern("match x:\n    case (y):\n        pass\n");
        pattern.Should().BeOfType<BindingPattern>().Which.Name.Name.Should().Be("y");
    }

    [Fact]
    public void ParenthesizedWildcard_IsTheWildcard()
    {
        var pattern = FirstCasePattern("match x:\n    case (_):\n        pass\n");
        pattern.Should().BeOfType<WildcardPattern>();
    }

    [Fact]
    public void ParenthesizedOrPattern_IsTheOrPattern()
    {
        var pattern = FirstCasePattern("match x:\n    case (1 | 2):\n        pass\n");
        pattern.Should().BeOfType<OrPattern>().Which.Alternatives.Should().HaveCount(2);
    }

    [Fact]
    public void TrailingComma_IsAOneElementTuplePattern()
    {
        var pattern = FirstCasePattern("match x:\n    case (y,):\n        pass\n");
        pattern.Should().BeOfType<TuplePattern>().Which.Elements.Should().ContainSingle()
            .Which.Should().BeOfType<BindingPattern>();
    }

    [Fact]
    public void TwoElements_IsATuplePattern()
    {
        var pattern = FirstCasePattern("match x:\n    case (a, b):\n        pass\n");
        pattern.Should().BeOfType<TuplePattern>().Which.Elements.Should().HaveCount(2);
    }

    [Fact]
    public void EmptyParens_IsAnEmptyTuplePattern()
    {
        var pattern = FirstCasePattern("match x:\n    case ():\n        pass\n");
        pattern.Should().BeOfType<TuplePattern>().Which.Elements.Should().BeEmpty();
    }

    [Fact]
    public void ParenthesizedGuardPattern_StaysAGuardPattern()
    {
        // RFC 3637 `(pattern if guard)` is parsed by the same production; the group rule must not
        // swallow it.
        var pattern = FirstCasePattern("match x:\n    case (y if y > 0):\n        pass\n");
        pattern.Should().BeOfType<GuardPattern>().Which.Inner.Should().BeOfType<BindingPattern>();
    }
}
