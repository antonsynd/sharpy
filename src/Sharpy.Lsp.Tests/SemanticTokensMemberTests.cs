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
            + "    b: Box? = Box()\n"
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
