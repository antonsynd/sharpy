using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// <see cref="LexerNs.Lexer.LiteralStateUnknown"/> (P22e decision 7, #2168): the lexer records that it
/// aborted while reading a literal that can span lines — any triple-quoted read, or an f-/t-string that is
/// triple-quoted or began on an earlier line — because error recovery then resumes on the next line as
/// code and the token stream's map of string lines is a guess for the rest of the document. A
/// single-quoted literal ends at its line and sets nothing. The fact changes no token or diagnostic.
/// </summary>
public class LiteralStateTests
{
    private readonly ITestOutputHelper _output;

    public LiteralStateTests(ITestOutputHelper output) => _output = output;

    /// <summary>Every string-literal prefix, spelled out (not read from the lexer's table — see <see cref="Rows_CoverEveryLexerPrefix"/>).</summary>
    private static readonly string[] Prefixes = { "", "d", "r", "dr", "b", "f", "t", "df" };

    private static readonly string[] TripleQuotes = { "\"\"\"", "'''" };

    public static IEnumerable<object[]> PrefixByTripleQuote()
        => Prefixes.SelectMany(p => TripleQuotes.Select(q => new object[] { p, q }));

    public static IEnumerable<object[]> AllPrefixes() => Prefixes.Select(p => new object[] { p });

    private static LexerNs.Lexer Lex(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        lexer.TokenizeAll();
        return lexer;
    }

    private static string Errors(LexerNs.Lexer lexer)
        => string.Join(" | ", lexer.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message} @{d.Line}:{d.Column}"));

    [Fact]
    public void Rows_CoverEveryLexerPrefix()
    {
        Prefixes.Should().BeEquivalentTo(LexerNs.Lexer.StringLiteralPrefixes,
            "a new string prefix needs its rows here: a new read path is only covered once a test cuts it");
    }

    // ---------------------------------------------------------------- the two cut shapes

    /// <summary>D5u: the document ends after the literal's first interior line.</summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuote))]
    public void CutAfterFirstInteriorLine_SetsTheFact(string prefix, string quotes)
    {
        var lexer = Lex($"def main():\n    s = {prefix}{quotes}\n        key: value\n");

        lexer.Diagnostics.HasErrors.Should().BeTrue();
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    /// <summary>
    /// D19: an opener inserted ABOVE an existing closed multi-line literal of the same kind. The new
    /// opener pairs with the old opener's quotes, the old content lexes as code and the old closer opens
    /// a literal that runs to end of input. Control: the document without the inserted line is clean.
    /// </summary>
    [Theory]
    [MemberData(nameof(PrefixByTripleQuote))]
    public void OpenerInsertedAboveAClosedLiteral_SetsTheFact(string prefix, string quotes)
    {
        var closed = $"def g() -> str:\n    s = {prefix}{quotes}\n        key: value\n    {quotes}\n    return s\n";
        var control = Lex(closed);
        control.Diagnostics.HasErrors.Should().BeFalse(Errors(control));
        control.LiteralStateUnknown.Should().BeFalse();

        var lexer = Lex($"def f() -> int:\n    {prefix}{quotes}\n    return 1\n" + closed);

        lexer.Diagnostics.HasErrors.Should().BeTrue();
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    // ---------------------------------------------------------------- aborts inside a CLOSED literal

    /// <summary>D20f (and its t/df twins): a lone <c>}</c> aborts inside a literal whose closer is in the text.</summary>
    [Theory]
    [InlineData("f")]
    [InlineData("t")]
    [InlineData("df")]
    public void UnmatchedBraceInsideAClosedTripleFString_SetsTheFact(string prefix)
    {
        var lexer = Lex($"def main():\n    s = {prefix}\"\"\"\n        }}\n        key: value\n    \"\"\"\n    print(s)\n");

        Errors(lexer).Should().Contain("Unmatched '}'");
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    /// <summary>D20b: a non-ASCII character aborts inside a closed <c>b"""</c>.</summary>
    [Fact]
    public void NonAsciiInsideAClosedTripleByteString_SetsTheFact()
    {
        var lexer = Lex("def main():\n    s = b\"\"\"\n        \u00e9\n        key: value\n    \"\"\"\n    print(s)\n");

        Errors(lexer).Should().Contain("bytes can only contain ASCII");
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    /// <summary>An invalid escape aborts inside a closed plain <c>"""</c> (the escape reader runs inside the read).</summary>
    [Fact]
    public void InvalidEscapeInsideAClosedTripleString_SetsTheFact()
    {
        var lexer = Lex("def main():\n    s = \"\"\"\n        \\xZZ\n        key: value\n    \"\"\"\n    print(s)\n");

        Errors(lexer).Should().Contain("Invalid hex escape");
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    /// <summary>A single-quoted f-string nested in a triple f-string's field: the enclosing context is triple.</summary>
    [Fact]
    public void AbortInASingleQuotedFStringNestedInATripleOne_SetsTheFact()
    {
        var lexer = Lex("def main():\n    s = f\"\"\"\n        {f\"{x\"}\n        key: value\n    \"\"\"\n    print(s)\n");

        lexer.Diagnostics.HasErrors.Should().BeTrue();
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    // ---------------------------------------------------------------- single-quoted literals set nothing

    /// <summary>
    /// An unterminated single-quoted literal of every prefix ends at its line: nothing is set, so an
    /// unfinished <c>x = "abc</c> does not switch anything off. Positive control: its triple-quoted twin,
    /// cut the same way, sets the fact.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllPrefixes))]
    public void UnterminatedSingleQuotedLiteral_SetsNothing(string prefix)
    {
        var single = Lex($"x = {prefix}\"abc\ny = 1\n");
        single.Diagnostics.HasErrors.Should().BeTrue();
        single.LiteralStateUnknown.Should().BeFalse(Errors(single));

        var twin = Lex($"x = {prefix}\"\"\"abc\ny = 1\n");
        twin.Diagnostics.HasErrors.Should().BeTrue();
        twin.LiteralStateUnknown.Should().BeTrue(Errors(twin));
    }

    /// <summary>
    /// <c>x = f"{a</c> being typed: the unclosed field is reported at its bracket and recovery resumes on
    /// the next line (#2022) — the string began on that line, so nothing is set.
    /// </summary>
    [Fact]
    public void SingleQuotedFStringWithAnUnclosedFieldOnItsLine_SetsNothing()
    {
        var lexer = Lex("def main():\n    x = f\"{a\n    y = 1\n");

        Errors(lexer).Should().Contain("was never closed");
        lexer.LiteralStateUnknown.Should().BeFalse(Errors(lexer));
    }

    /// <summary>
    /// <c>fstrings/multiline_hole_2022</c> cut after <c>names</c>: the hole's innermost open bracket is
    /// the <c>(</c> of <c>join(</c>, on the string's start line, and the hole crossed a line break inside
    /// it before the source ended — the line after the bracket is the hole's, not code: set. Control,
    /// same prefix: the field's own <c>{</c> is the innermost bracket (the field being typed), nothing.
    /// </summary>
    [Theory]
    [InlineData("f")]
    [InlineData("t")]
    [InlineData("df")]
    public void SingleQuotedHoleThatCrossedALineInsideABracketOnItsStartLine_SetsTheFact(string prefix)
    {
        var lexer = Lex($"def main():\n    print({prefix}\"{{', '.join(\n        names\n");

        Errors(lexer).Should().Contain("'(' was never closed @2:");
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));

        var control = Lex($"def main():\n    x = {prefix}\"{{a\n    y = 1\n");
        Errors(control).Should().Contain("'{' was never closed @2:");
        control.LiteralStateUnknown.Should().BeFalse(Errors(control));
    }

    /// <summary>A single-quoted f-string whose field crossed a line break (PEP 701) before the abort: set.</summary>
    [Fact]
    public void SingleQuotedFStringThatCrossedALineBreak_SetsTheFact()
    {
        var lexer = Lex("def main():\n    s = f\"{a +\n        b} tail\n    y = 1\n");

        Errors(lexer).Should().Contain("Unterminated f-string");
        lexer.LiteralStateUnknown.Should().BeTrue(Errors(lexer));
    }

    /// <summary>An abort outside every literal sets nothing.</summary>
    [Fact]
    public void AbortOutsideAnyLiteral_SetsNothing()
    {
        var lexer = Lex("def main():\n    s = \"\"\"\n        key: value\n    \"\"\"\n    x = 1__2\n");

        Errors(lexer).Should().Contain("consecutive underscores");
        lexer.LiteralStateUnknown.Should().BeFalse(Errors(lexer));
    }

    // ---------------------------------------------------------------- the corpus

    /// <summary>
    /// Diagnostic-neutral on the corpus: every <c>.spy</c> under the compiler's <c>TestFixtures/</c> that
    /// lexes clean has the fact false. Positive control, generated from the same corpus: each of its
    /// triple-quoted literals with a complete interior line, cut after that line, sets it.
    /// </summary>
    [Fact]
    public void Corpus_CleanFixturesSetNothing_AndEveryCutMultiLineLiteralSetsTheFact()
    {
        var root = FixtureRoots.CompilerTests.Path;
        var files = Directory.EnumerateFiles(root, "*.spy", SearchOption.AllDirectories)
            .Where(f => !Sharpy.Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(root, f)))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        var clean = 0;
        var cutsByKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var lexer = new LexerNs.Lexer(source);
            var tokens = lexer.TokenizeAll();
            if (lexer.Diagnostics.HasErrors)
                continue;
            clean++;
            var name = Path.GetRelativePath(FixtureRoots.CompilerTests.Path, file);
            if (lexer.LiteralStateUnknown)
                failures.Add($"{name}: lexes clean but the fact is set");

            foreach (var (kind, opener, closerStart) in MultiLineLiterals(source, tokens))
            {
                var openerLineEnd = source.IndexOf('\n', opener);
                var interiorLineEnd = openerLineEnd < 0 ? -1 : source.IndexOf('\n', openerLineEnd + 1);
                if (interiorLineEnd < 0 || interiorLineEnd >= closerStart)
                    continue;   // no complete interior line before the closer
                cutsByKind[kind] = cutsByKind.GetValueOrDefault(kind) + 1;
                var cut = Lex(source[..(interiorLineEnd + 1)]);
                if (!cut.LiteralStateUnknown)
                    failures.Add($"{name}: '{kind}' literal at offset {opener} cut after its first interior line, fact not set ({Errors(cut)})");
            }
        }

        _output.WriteLine($"files {files.Count}, lex clean {clean}, cuts {string.Join(", ", cutsByKind.Select(k => $"{(k.Key.Length == 0 ? "plain" : k.Key)}={k.Value}"))}");
        failures.Should().BeEmpty();
        clean.Should().BeGreaterThan(100, "the corpus is the compiler's fixture tree");
        cutsByKind.Keys.Should().Contain(new[] { "", "f" }, "the positive control must reach the plain and f-string read paths");
    }

    /// <summary>(prefix, opener offset, offset of the closing delimiter) of every triple-quoted literal.</summary>
    private static IEnumerable<(string Kind, int Opener, int CloserStart)> MultiLineLiterals(string source, List<LexerNs.Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Type is TokenType.String or TokenType.RawString or TokenType.ByteString && token.SourceLength is { } length)
            {
                var quote = source.IndexOfAny(new[] { '"', '\'' }, token.Position);
                if (quote + 2 < source.Length && source[quote + 1] == source[quote] && source[quote + 2] == source[quote]
                    && length >= quote - token.Position + 6)
                    yield return (source[token.Position..quote], token.Position, token.Position + length - 3);
            }
            else if (token.Type == TokenType.FStringStart
                && (token.Value.EndsWith("\"\"\"", StringComparison.Ordinal) || token.Value.EndsWith("'''", StringComparison.Ordinal)))
            {
                var depth = 0;
                for (var j = i; j < tokens.Count; j++)
                {
                    if (tokens[j].Type == TokenType.FStringStart)
                        depth++;
                    else if (tokens[j].Type == TokenType.FStringEnd && --depth == 0)
                    {
                        yield return (token.Value[..^3], token.Position, tokens[j].Position);
                        break;
                    }
                }
            }
        }
    }
}
