using FluentAssertions;
using Sharpy.TestInfrastructure.Lexing;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// End of file × construct (#2300 row C6, with C2/C3 riding along, P68 Phase 2). Every construct of a closed literal
/// roster — every operator/delimiter spelling <c>ReadOperatorOrDelimiter</c> reads, every string prefix × quote closed
/// and unterminated, every number form, a comment, a backtick name closed and unterminated, a keyword, an identifier,
/// a lone backslash, an open block and the open f-string field/spec/conversion and escape-at-EOF shapes — is lexed
/// at the end of the source under seven endings: none, <c>\n</c>, <c>\r\n</c>, <c>\r</c>, <c>\n\n</c>, trailing
/// spaces and a trailing comment. Each construct sits after <c>x = </c> (statement constructs stand alone) and, where
/// a token can follow it on its line, is ALSO lexed followed by <c> b</c> — R1 of
/// <see cref="TokenPositionReconstruction"/> is vacuous at column 1, so a dropped column advance inside the construct
/// is seen on that following token. Every cell must:
/// <list type="bullet">
/// <item>let no exception escape <see cref="LexerNs.Lexer.TokenizeAll"/> (an <c>IndexOutOfRangeException</c> past the
/// source end is the class: the pilot's <c>dr"abc</c> mutant);</item>
/// <item>reconstruct (<see cref="TokenPositionReconstruction.Violations(string, LexerNs.Lexer, IReadOnlyList{LexerNs.Token})"/> empty);</item>
/// <item>agree with the same construct's <c>\n</c> cell on the (Type, Value) sequence — a <c>Newline</c> followed
/// only by <c>Dedent</c>/<c>Eof</c> dropped — and on the multiset of diagnostic codes.</item>
/// </list>
/// A construct whose text ends in a backslash (<see cref="Construct.EndingIsOperand"/>: the lone <c>\</c>,
/// <c>"abc\</c>, <c>b"\x4</c>) reads the ending's first character as its operand — <c>\</c> + break is a
/// continuation, <c>\</c> + space is not — so for it only the three single line-break endings are held equal (the C3
/// invariance); its other endings still must not throw and must reconstruct.
/// <para>
/// Roster totality: every operator/delimiter <see cref="TokenType"/> (<c>Plus</c> … <c>Backslash</c>) is the first
/// token of at least one construct lexed alone — the enum sweep is the positive control that the literal list is
/// complete; the list, not the enum, is the anchor.
/// </para>
/// </summary>
public class EofConstructRosterTests
{
    private readonly ITestOutputHelper _output;

    public EofConstructRosterTests(ITestOutputHelper output) => _output = output;

    /// <param name="Name">The construct, for the report.</param>
    /// <param name="Text">Its source text, ending at the construct.</param>
    /// <param name="Followable">True when a token can follow it on its line (an unterminated literal or comment eats the rest).</param>
    /// <param name="Statement">True when it stands alone rather than after <c>x = </c>.</param>
    /// <param name="EndingIsOperand">True when its text ends in a backslash that reads the ending's first character.</param>
    public sealed record Construct(string Name, string Text, bool Followable = true, bool Statement = false, bool EndingIsOperand = false);

    private static readonly string[] OperatorSpellings =
    {
        "+", "-", "*", "/", "//", "%", "**", "==", "!=", "!", "<", ">", "<=", ">=", "&", "|", "^", "~", "<<", ">>",
        "=", ":=", "+=", "-=", "*=", "/=", "//=", "%=", "**=", "&=", "|=", "^=", "<<=", ">>=", "??=", "@=",
        "?", "?.", "...", "|>", "(", ")", "[", "]", "{", "}", ",", ":", ";", ".", "->", "=>", "@",
    };

    private static readonly string[] StringPrefixes = { "", "f", "t", "r", "b", "d", "dr", "df" };
    private static readonly string[] Quotes = { "'", "\"", "'''", "\"\"\"" };

    private static readonly string[] Numbers = { "42", "3.14", ".5", ".5e-3", "1e5", "0xFF", "0b101", "0o755", "42L", "3.14f", "0xFFu" };

    public static readonly IReadOnlyList<Construct> Constructs = BuildConstructs();

    private static List<Construct> BuildConstructs()
    {
        var constructs = new List<Construct>();
        constructs.AddRange(OperatorSpellings.Select(op => new Construct($"op {op}", op)));
        constructs.Add(new Construct("backslash", "\\", EndingIsOperand: true));
        foreach (var prefix in StringPrefixes)
        {
            foreach (var quote in Quotes)
            {
                constructs.Add(new Construct($"{prefix}{quote} closed", $"{prefix}{quote}abc{quote}"));
                constructs.Add(new Construct($"{prefix}{quote} unterminated", $"{prefix}{quote}abc", Followable: false));
            }
        }
        constructs.Add(new Construct("d\"\"\" closed over lines", "d\"\"\"\n    k\n    \"\"\""));
        constructs.AddRange(Numbers.Select(n => new Construct($"number {n}", n)));
        constructs.Add(new Construct("comment", "# comment", Followable: false, Statement: true));
        constructs.Add(new Construct("backtick name closed", "`a b`"));
        constructs.Add(new Construct("backtick name unterminated", "`a b", Followable: false));
        constructs.Add(new Construct("keyword", "union"));
        constructs.Add(new Construct("identifier", "name"));
        constructs.Add(new Construct("open block", "if x:\n    y", Statement: true));
        constructs.Add(new Construct("f-string open field", "f\"{x", Followable: false));
        constructs.Add(new Construct("f-string open spec", "f\"{x:>4", Followable: false));
        constructs.Add(new Construct("f-string open conversion", "f\"{x!r", Followable: false));
        constructs.Add(new Construct("escape at EOF", "\"abc\\", Followable: false, EndingIsOperand: true));
        constructs.Add(new Construct("short hex escape at EOF", "b\"\\x4", Followable: false, EndingIsOperand: true));
        constructs.Add(new Construct("dedented unterminated", "d\"\"\"\n  k", Followable: false));
        return constructs;
    }

    public static readonly IReadOnlyList<(string Name, string Text)> Endings = new[]
    {
        ("none", ""), ("LF", "\n"), ("CRLF", "\r\n"), ("CR", "\r"), ("LF LF", "\n\n"), ("trailing spaces", "   "), ("trailing comment", "  # c"),
    };

    private static readonly HashSet<string> LineBreakEndings = new(StringComparer.Ordinal) { "LF", "CRLF", "CR" };

    private sealed record Cell(string Name, string Source, LexerNs.Lexer Lexer, List<LexerNs.Token>? Tokens, Exception? Thrown);

    private static Cell LexCell(string name, string source)
    {
        var lexer = new LexerNs.Lexer(source);
        try
        {
            return new Cell(name, source, lexer, lexer.TokenizeAll(), null);
        }
        catch (Exception ex)
        {
            return new Cell(name, source, lexer, null, ex);
        }
    }

    private static List<string> TokenSequence(List<LexerNs.Token> tokens)
    {
        var sequence = tokens.Select(t => $"{t.Type}|{t.Value}").ToList();
        var newline = tokens.FindLastIndex(t => t.Type == TokenType.Newline);
        if (newline >= 0 && tokens.Skip(newline + 1).All(t => t.Type is TokenType.Dedent or TokenType.Eof))
            sequence.RemoveAt(newline);
        return sequence;
    }

    private static List<string> Codes(LexerNs.Lexer lexer) =>
        lexer.Diagnostics.GetAll().Select(d => d.Code ?? "-").OrderBy(c => c, StringComparer.Ordinal).ToList();

    private static string Shown(string text) =>
        text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    [Fact]
    public void EveryConstruct_LexesTheSameAtEof_WithAndWithoutATrailingNewline()
    {
        var violations = new List<(string Scope, string Violation)>();
        var cells = 0;
        foreach (var construct in Constructs)
        {
            var scope = $"roster {construct.Name}";
            var line = construct.Statement ? construct.Text : "x = " + construct.Text;
            var placements = construct.Followable
                ? new[] { ("at end", line), ("followed", line + " b") }
                : new[] { ("at end", line) };
            foreach (var (placement, text) in placements)
            {
                var lexed = Endings.ToDictionary(e => e.Name, e => LexCell($"{construct.Name} [{placement}] {e.Name}", text + e.Text));
                cells += lexed.Count;
                foreach (var cell in lexed.Values)
                {
                    if (cell.Thrown != null)
                    {
                        violations.Add((scope, $"THROW {cell.Thrown.GetType().Name} {cell.Name} :: '{Shown(cell.Source)}': {cell.Thrown.Message}"));
                        continue;
                    }
                    violations.AddRange(TokenPositionReconstruction.Violations(cell.Source, cell.Lexer, cell.Tokens!)
                        .Select(v => (scope, $"{v} :: {cell.Name} '{Shown(cell.Source)}'")));
                }

                var reference = lexed["LF"];
                if (reference.Tokens == null)
                    continue;
                foreach (var (endingName, _) in Endings)
                {
                    var cell = lexed[endingName];
                    if (cell.Tokens == null || endingName == "LF")
                        continue;
                    if (construct.EndingIsOperand && !LineBreakEndings.Contains(endingName))
                        continue;
                    var expectedTokens = TokenSequence(reference.Tokens);
                    var actualTokens = TokenSequence(cell.Tokens);
                    if (!expectedTokens.SequenceEqual(actualTokens))
                        violations.Add((scope, $"TOKENS {cell.Name} '{Shown(cell.Source)}': LF [{Shown(string.Join(" ", expectedTokens))}] this [{Shown(string.Join(" ", actualTokens))}]"));
                    var expectedCodes = Codes(reference.Lexer);
                    var actualCodes = Codes(cell.Lexer);
                    if (!expectedCodes.SequenceEqual(actualCodes))
                        violations.Add((scope, $"CODES {cell.Name} '{Shown(cell.Source)}': LF [{string.Join(" ", expectedCodes)}] this [{string.Join(" ", actualCodes)}]"));
                }
            }
        }

        var (rows, malformed) = LexerPositionAllowlist.Read();
        var (remaining, stale, allowlisted) = LexerPositionAllowlist.Apply(rows, "roster", violations);
        var failures = malformed.Concat(stale).Concat(remaining).ToList();
        _output.WriteLine($"EOF-ROSTER constructs {Constructs.Count} endings {Endings.Count} cells {cells} violations {remaining.Count} allowlisted {allowlisted}");
        foreach (var failure in failures.Take(200))
            _output.WriteLine("EOF-ROSTER " + failure);

        failures.Should().BeEmpty("a position defect outside the contract is a findings-ledger line and a row in {0} citing its issue",
            LexerPositionAllowlist.FileName);
    }

    [Fact]
    public void Roster_HasAConstructForEveryOperatorAndDelimiterTokenType()
    {
        var firstTypes = Constructs.Select(c =>
        {
            var tokens = new LexerNs.Lexer(c.Text).TokenizeAll();
            return tokens[0].Type;
        }).ToHashSet();

        var operatorTypes = Enum.GetValues<TokenType>().Where(t => t >= TokenType.Plus && t <= TokenType.Backslash).ToList();
        operatorTypes.Should().Contain(new[] { TokenType.Plus, TokenType.RightShift, TokenType.NullCoalesceAssign, TokenType.Backslash },
            "the range Plus … Backslash is the operator/delimiter block of TokenType");

        operatorTypes.Where(t => !firstTypes.Contains(t)).Should().BeEmpty(
            "every operator/delimiter token type needs a roster construct that starts with it");
    }
}
