using FluentAssertions;
using Sharpy.Compiler.Lexer;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// <see cref="IndentationService.BuildIndentMap"/> surfaces the lexer's
/// <c>LiteralStateUnknown</c> (P22e decision 7, #2168) next to the map it computes.
/// </summary>
public class IndentationServiceLiteralStateTests
{
    /// <summary>D19: a docstring opened ABOVE an existing multi-line string.</summary>
    private const string D19 =
        "def f() -> int:\n    \"\"\"\n    return 1\ndef g() -> str:\n    s = \"\"\"\n        key: value\n    \"\"\"\n    return s\n";

    /// <summary>D19 without the inserted opener: lexes clean.</summary>
    private const string Clean =
        "def f() -> int:\n    return 1\ndef g() -> str:\n    s = \"\"\"\n        key: value\n    \"\"\"\n    return s\n";

    [Fact]
    public void BuildIndentMap_SurfacesTheLexersLiteralState()
    {
        IndentationService.BuildIndentMap(D19).LiteralStateUnknown.Should().BeTrue();
        IndentationService.BuildIndentMap(Clean).LiteralStateUnknown.Should().BeFalse();
    }

    // ---- P22f (#2271): a literal lost WITHOUT a lexer abort sets the fact; a lex that loses none does not.
    // The documents are FormattingFallbackTests' constants; the flag names the mechanism that lost it.

    public static TheoryData<string, string, LiteralLoss> LiteralLostWithoutAnAbort => new()
    {
        { "N1", FormattingFallbackTests.N1, LiteralLoss.UnreadRemainder },
        { "N2", FormattingFallbackTests.N2, LiteralLoss.RePairedCloser },
        { "N4", FormattingFallbackTests.N4, LiteralLoss.DroppedOpener },
        { "X6", FormattingFallbackTests.X6, LiteralLoss.DroppedOpener },
    };

    [Theory]
    [MemberData(nameof(LiteralLostWithoutAnAbort))]
    public void BuildIndentMap_ALiteralLostWithoutAnAbort_IsUnknown(string name, string document, LiteralLoss mechanism)
    {
        IndentationService.BuildIndentMap(document).LiteralStateUnknown.Should().BeTrue(name);

        var lexer = new Lexer(document);
        lexer.TokenizeAll();
        lexer.LiteralLoss.Should().Be(mechanism, "{0} loses its literal by that mechanism alone, without an abort inside a read", name);
    }

    public static TheoryData<string, string> NoLiteralLost => new()
    {
        { "N4q", FormattingFallbackTests.N4q },
        { "N4t", FormattingFallbackTests.N4t },
        { "N1c", FormattingFallbackTests.N1c },
    };

    [Theory]
    [MemberData(nameof(NoLiteralLost))]
    public void BuildIndentMap_ALexThatLosesNoLiteral_IsKnown(string name, string document)
    {
        IndentationService.BuildIndentMap(document).LiteralStateUnknown.Should().BeFalse(name);
    }
}
