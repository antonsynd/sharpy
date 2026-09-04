using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using static Sharpy.Lsp.Handlers.SharpySemanticTokensHandler;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// #1376: the tokenizer walked a <c>MemberAccess</c>'s object and emitted nothing for the member
/// name, so <c>math.pi</c>, <c>obj.field</c> and <c>Module.func</c> all rendered as plain
/// identifiers.
/// </summary>
/// <remarks>
/// <para>
/// These cells need a semantic result, not a parse: <c>SemanticTokensTests.CollectTokensFrom</c>
/// calls <c>_api.Parse</c> and the two-argument <c>CollectTokens</c> overload, which passes
/// <c>semanticQuery: null</c> — under which this arm deliberately emits nothing.
/// </para>
/// <para>
/// The issue's acceptance sketch proposed classifying from <c>GetMemberAccessResolution</c>.
/// Measured, that map answers only static/const/enum/union shapes and returns null for every case
/// below, so the classification comes from the member expression's own type instead.
/// </para>
/// </remarks>
public class SemanticTokensMemberTests
{
    private readonly CompilerApi _api = new();

    private System.Collections.Generic.List<RawToken> CollectAnalyzed(string source)
    {
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue("source should analyse cleanly: {0}",
            string.Join("; ", analysis.Diagnostics.Select(d => d.Code + " " + d.Message)));
        analysis.Ast.Should().NotBeNull();
        analysis.SemanticQuery.Should().NotBeNull(
            "the member arm is a no-op without a query, so a null one would make every "
            + "assertion below vacuous");

        var tokens = new System.Collections.Generic.List<RawToken>();
        CollectTokens(analysis.Ast!.Body, tokens, analysis.SemanticQuery);
        return tokens;
    }

    private const string ClassSource =
        "class Inner:\n"
        + "    n: int = 1\n"
        + "\n"
        + "class Counter:\n"
        + "    total: int = 0\n"
        + "    inner: Inner = Inner()\n"
        + "\n"
        + "    def bump(self) -> int:\n"
        + "        return self.total\n"
        + "\n"
        + "def main() -> None:\n"
        + "    c: Counter = Counter()\n"
        + "    print(c.total)\n"
        + "    print(c.bump())\n"
        + "    print(c.inner.n)\n";

    [Fact]
    public void InstanceField_IsTokenizedAsAProperty()
    {
        var tokens = CollectAnalyzed(ClassSource);

        // `    print(c.total)` is 0-based line 12; `total` starts at 0-based character 12.
        tokens.Should().ContainSingle(t => t.Line == 12 && t.Col == 12 && t.Length == 5)
            .Which.TokenType.Should().Be(TProperty);
    }

    [Fact]
    public void InstanceMethod_IsTokenizedAsAMethod()
    {
        var tokens = CollectAnalyzed(ClassSource);

        // `    print(c.bump())` is 0-based line 13; `bump` starts at character 12.
        tokens.Should().ContainSingle(t => t.Line == 13 && t.Col == 12 && t.Length == 4)
            .Which.TokenType.Should().Be(TMethod);
    }

    [Fact]
    public void ChainedMemberAccess_TokenizesBothMembers()
    {
        // The outer member's object is itself a MemberAccess. Both must be reached, or a chain
        // colors only its first hop — the recursion is the thing under test here.
        var tokens = CollectAnalyzed(ClassSource);

        // `    print(c.inner.n)` is 0-based line 14: `inner` at char 12, `n` at char 18.
        tokens.Should().ContainSingle(t => t.Line == 14 && t.Col == 12 && t.Length == 5)
            .Which.TokenType.Should().Be(TProperty);
        tokens.Should().ContainSingle(t => t.Line == 14 && t.Col == 18 && t.Length == 1)
            .Which.TokenType.Should().Be(TProperty);
    }

    [Fact]
    public void NullConditionalAccess_TokenizesTheMemberPastBothSeparatorCharacters()
    {
        // `?.` spends one more character than `.`, so an arm that assumes +1 lands on the '.' and
        // reports a member one column to the left, overlapping the operator.
        var source =
            "class Box:\n"
            + "    value: int = 1\n"
            + "\n"
            + "def main() -> None:\n"
            + "    b: Box? = Some(Box())\n"
            + "    print(b?.value)\n";

        var tokens = CollectAnalyzed(source);

        // `    print(b?.value)` is 0-based line 5; `value` starts at character 13.
        tokens.Should().ContainSingle(t => t.Line == 5 && t.Length == 5)
            .Which.Col.Should().Be(13,
                "the member begins two characters after the object ends, not one");
    }

    [Fact]
    public void EscapedMemberName_SpansItsBackticks()
    {
        // The recorded token extent includes both backticks (cb429fdc1); the computed extent must
        // agree with it, or an escaped member is underlined one character short at each end.
        var source =
            "class Widget:\n"
            + "    `event`: int = 1\n"
            + "\n"
            + "def main() -> None:\n"
            + "    w: Widget = Widget()\n"
            + "    print(w.`event`)\n";

        var tokens = CollectAnalyzed(source);

        // `    print(w.\`event\`)` is 0-based line 5; the opening backtick is at character 12 and
        // the extent covers `event` plus both backticks.
        tokens.Should().ContainSingle(t => t.Line == 5 && t.Col == 12)
            .Which.Length.Should().Be(7, "backtick + event + backtick");
    }

    // === Whitespace around the separator (#1503) ===
    //
    // The lexer skips whitespace unconditionally, so `obj . field` is exactly as legal as
    // `obj.field`. The arm used to compute the member's start from the receiver's end plus a
    // separator width, which lands the token on the GAP the moment anything sits around the dot.
    // These cells were written red against the pre-fix handler and are the first EXECUTION of the
    // defect — the issue derived it statically.

    /// <summary>The specimen, with <paramref name="expression"/> as the argument on 0-based line 5.</summary>
    private static string PaddedSource(string expression) =>
        "class Counter:\n"
        + "    total: int = 0\n"
        + "\n"
        + "def main() -> None:\n"
        + "    c: Counter = Counter()\n"
        + $"    print({expression})\n";

    [Theory]
    // `    print(` is 10 characters, so the receiver `c` sits at 0-based character 10.
    [InlineData("c.total", 12, "contiguous — the control")]
    [InlineData("c . total", 14, "padded on both sides of the dot")]
    [InlineData("c .total", 13, "padded before the dot")]
    [InlineData("c. total", 13, "padded after the dot")]
    public void PaddedMemberAccess_TokenLandsOnTheMemberNotTheGap(
        string expression, int expectedCol, string shape)
    {
        var tokens = CollectAnalyzed(PaddedSource(expression));

        tokens.Should().ContainSingle(t => t.Line == 5 && t.Length == 5)
            .Which.Col.Should().Be(expectedCol,
                $"{shape}: the token belongs on `total`, wherever the whitespace put it");
    }

    [Fact]
    public void PaddedNullConditionalAccess_TokenLandsOnTheMember()
    {
        // Both variables at once: the separator is two characters AND padded on both sides.
        var source =
            "class Box:\n"
            + "    value: int = 1\n"
            + "\n"
            + "def main() -> None:\n"
            + "    b: Box? = Some(Box())\n"
            + "    print(b ?. value)\n";

        var tokens = CollectAnalyzed(source);

        // `    print(b ?. value)`: `b` at 10, `?.` at 12-13, `value` at 15.
        tokens.Should().ContainSingle(t => t.Line == 5 && t.Length == 5)
            .Which.Col.Should().Be(15);
    }

    [Fact]
    public void PaddedEscapedMemberName_SpansItsBackticksAtTheRightColumn()
    {
        var source =
            "class Widget:\n"
            + "    `event`: int = 1\n"
            + "\n"
            + "def main() -> None:\n"
            + "    w: Widget = Widget()\n"
            + "    print(w . `event`)\n";

        var tokens = CollectAnalyzed(source);

        // `    print(w . \`event\`)`: `w` at 10, `.` at 12, the opening backtick at 14.
        tokens.Should().ContainSingle(t => t.Line == 5 && t.Length == 7)
            .Which.Col.Should().Be(14, "backtick + event + backtick, starting where the token does");
    }

    [Fact]
    public void MultiLineMemberChain_TokenLandsOnTheMembersOwnLine()
    {
        // The handler used to bail here, because there was no honest way to place the token: the
        // arithmetic was anchored to the receiver, which is on another line entirely. With the
        // member's own position recorded there is nothing left to guess (#1503).
        var source =
            "class Counter:\n"
            + "    total: int = 0\n"
            + "\n"
            + "def main() -> None:\n"
            + "    c: Counter = Counter()\n"
            + "    print((c\n"
            + "        .total))\n";

        var tokens = CollectAnalyzed(source);

        // `        .total))` is 0-based line 6; `total` starts at character 9.
        tokens.Should().ContainSingle(t => t.Line == 6 && t.Length == 5)
            .Which.Col.Should().Be(9, "the member is on its own line, not the receiver's");
    }

    [Fact]
    public void WithoutASemanticQuery_NoMemberTokenIsEmitted()
    {
        // Tokenize falls back to a parse-only result when analysis is unavailable. Guessing a kind
        // from syntax alone would color methods as properties roughly half the time, so the arm
        // stays silent — and this pins that, so the fallback path cannot start guessing later.
        var parseResult = _api.Parse(ClassSource);
        parseResult.Success.Should().BeTrue();

        var tokens = new System.Collections.Generic.List<RawToken>();
        CollectTokens(parseResult.Ast!.Body, tokens);

        tokens.Should().NotContain(t => t.Line == 12 && t.Col == 12 && t.Length == 5,
            "no query means no classification, and an unclassified guess is worse than none");
    }
}
