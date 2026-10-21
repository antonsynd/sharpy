using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// #2300 row C7 (P68): every keyword the SPEC lists is pinned to its <see cref="TokenType"/> inside
/// the Lexer namespaces. <see cref="Roster"/> is a literal spelled from
/// <c>docs/language_specification/keywords.md</c>'s three tables — Hard Keywords (49 rows; the stale
/// <c>to</c> row, removed in 0.8.0 per <c>type_casting.md</c>, is gone), Reserved Rejected Keywords
/// (<c>global</c>, <c>nonlocal</c>) and Future Keywords (<c>defer</c>, <c>do</c>) — plus the
/// contextual <c>self</c>, which is in <c>Lexer.KeywordNames</c> but always lexes as an
/// <see cref="TokenType.Identifier"/>. It is anchored to the spec, not to the lexer's
/// <c>Keywords</c> table or the <see cref="TokenType"/> enum: a roster read from the table under
/// test would make a missing keyword invisible.
///
/// <para><b>Why <c>self</c> is a row.</b> Its consumers read <c>KeywordNames</c>, not a token type:
/// <c>Parser.IsReservedWordToken</c> (<c>Parser.cs:410</c>) skips it because it lexes as an
/// Identifier, and the LSP rename handler (<c>RenameHandler.cs:259</c>) backtick-escapes a new name
/// that is in <c>KeywordNames</c> — dropping <c>self</c> from the set would let a rename write a bare
/// <c>self</c>.</para>
///
/// <para><b>Whole-suite control.</b> <c>Integration.ReservedWordAsNameMatrixTests.Roster_IsTheLexerKeywordTable</c>
/// pins the lexer's 53-name table (minus <c>self</c>) for the reserved-word-as-name matrix; it sits
/// outside the Stryker filter (<c>Sharpy.Compiler.Tests.Lexer.|Sharpy.Compiler.Tests.Properties.Lexer.</c>),
/// which is why this roster exists inside it.</para>
///
/// <para><b>Next editors.</b> P69 (<c>out</c>) and P70 (<c>property</c>) respell those keywords by
/// editing THIS table in one commit each, together with the spec row — they cite it; they never
/// widen it with an exemption.</para>
/// </summary>
public class KeywordRosterTests
{
    private const string Hard = "Hard Keywords";
    private const string Rejected = "Reserved Rejected Keywords";
    private const string Future = "Future Keywords";
    private const string Contextual = "contextual — KeywordNames only";

    private readonly ITestOutputHelper _output;

    public KeywordRosterTests(ITestOutputHelper output) => _output = output;

    /// <summary>keywords.md's tables, in the spec's row order.</summary>
    private static readonly (string Keyword, TokenType Type, string SpecTable)[] Roster =
    {
        ("and", TokenType.And, Hard),
        ("as", TokenType.As, Hard),
        ("assert", TokenType.Assert, Hard),
        ("auto", TokenType.Auto, Hard),
        ("break", TokenType.Break, Hard),
        ("case", TokenType.Case, Hard),
        ("class", TokenType.Class, Hard),
        ("const", TokenType.Const, Hard),
        ("continue", TokenType.Continue, Hard),
        ("def", TokenType.Def, Hard),
        ("elif", TokenType.Elif, Hard),
        ("else", TokenType.Else, Hard),
        ("enum", TokenType.Enum, Hard),
        ("event", TokenType.Event, Hard),
        ("except", TokenType.Except, Hard),
        ("False", TokenType.False, Hard),
        ("finally", TokenType.Finally, Hard),
        ("for", TokenType.For, Hard),
        ("from", TokenType.From, Hard),
        ("if", TokenType.If, Hard),
        ("import", TokenType.Import, Hard),
        ("in", TokenType.In, Hard),
        ("interface", TokenType.Interface, Hard),
        ("is", TokenType.Is, Hard),
        ("lambda", TokenType.Lambda, Hard),
        ("let", TokenType.Let, Hard),
        ("match", TokenType.Match, Hard),
        ("maybe", TokenType.Maybe, Hard),
        ("None", TokenType.None, Hard),
        ("not", TokenType.Not, Hard),
        ("or", TokenType.Or, Hard),
        ("pass", TokenType.Pass, Hard),
        ("property", TokenType.Property, Hard),
        ("raise", TokenType.Raise, Hard),
        ("return", TokenType.Return, Hard),
        ("struct", TokenType.Struct, Hard),
        ("True", TokenType.True, Hard),
        ("try", TokenType.Try, Hard),
        ("type", TokenType.Type, Hard),
        ("while", TokenType.While, Hard),
        ("with", TokenType.With, Hard),
        ("yield", TokenType.Yield, Hard),
        ("async", TokenType.Async, Hard),
        ("await", TokenType.Await, Hard),
        ("del", TokenType.Del, Hard),
        ("delegate", TokenType.Delegate, Hard),
        ("super", TokenType.Super, Hard),
        ("union", TokenType.Union, Hard),
        ("Self", TokenType.SelfType, Hard),

        ("global", TokenType.Global, Rejected),
        ("nonlocal", TokenType.Nonlocal, Rejected),

        ("defer", TokenType.Defer, Future),
        ("do", TokenType.Do, Future),

        ("self", TokenType.Identifier, Contextual),
    };

    public static IEnumerable<object[]> Rows() =>
        Roster.Select(r => new object[] { r.Keyword, r.Type, r.SpecTable });

    private static List<(TokenType Type, string Value)> Lex(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse($"source {source.Replace("\n", "\\n", System.StringComparison.Ordinal)} lexes clean");
        return tokens.Select(t => (t.Type, t.Value)).ToList();
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void Keyword_Alone_LexesToItsTokenType(string keyword, TokenType type, string specTable)
    {
        Lex(keyword + "\n").Should().Equal(
            new List<(TokenType, string)> { (type, keyword), (TokenType.Newline, "\n"), (TokenType.Eof, "") },
            $"'{keyword}' ({specTable}) on a line of its own");
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void Keyword_BetweenIdentifiers_LexesToItsTokenType(string keyword, TokenType type, string specTable)
    {
        Lex($"a {keyword} b\n").Should().Equal(
            new List<(TokenType, string)> { (TokenType.Identifier, "a"), (type, keyword), (TokenType.Identifier, "b"), (TokenType.Newline, "\n"), (TokenType.Eof, "") },
            $"'{keyword}' ({specTable}) between two identifiers");
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void Keyword_AtEofWithoutNewline_LexesToItsTokenType(string keyword, TokenType type, string specTable)
    {
        Lex($"a {keyword}").Should().Equal(
            new List<(TokenType, string)> { (TokenType.Identifier, "a"), (type, keyword), (TokenType.Eof, "") },
            $"'{keyword}' ({specTable}) as the last token of a source with no trailing newline");
    }

    [Fact]
    public void KeywordNames_IsTheSpecRoster()
    {
        Roster.Select(r => r.Keyword).Should().OnlyHaveUniqueItems();
        Roster.Count(r => r.SpecTable == Hard).Should().Be(49, "keywords.md Hard Keywords");
        Roster.Count(r => r.SpecTable == Rejected).Should().Be(2, "keywords.md Reserved Rejected Keywords");
        Roster.Count(r => r.SpecTable == Future).Should().Be(2, "keywords.md Future Keywords");
        Roster.Count(r => r.SpecTable == Contextual).Should().Be(1, "self");

        LexerNs.Lexer.KeywordNames.Should().BeEquivalentTo(Roster.Select(r => r.Keyword),
            "the lexer's keyword set is exactly the spec's three tables plus the contextual self");

        _output.WriteLine($"KEYWORD-ROSTER rows {Roster.Length}");
    }
}
