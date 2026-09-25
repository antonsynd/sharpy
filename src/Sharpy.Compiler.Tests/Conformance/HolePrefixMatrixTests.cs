using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SToken = Sharpy.Compiler.Lexer.Token;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// One string-literal prefix dispatch (#2010): a string literal lexes identically at statement level
/// and inside a replacement field, for every prefix the main loop knows × every quote kind. The hole
/// tokenizer used to know only <c>f</c> and bare quotes, so <c>t'…'</c>, <c>r'…'</c>, <c>b'…'</c>,
/// <c>d'…'</c>, <c>dr'…'</c>, <c>df'…'</c> inside a hole lexed as an identifier followed by a string
/// (SPY0104; <c>r'\d'</c> → SPY0004). Same-quote reuse (<c>f"{"a"}"</c>) already worked.
///
/// <para>The prefix list below is written out literally and pinned to <see cref="SLexer.StringLiteralPrefixes"/>
/// (a prefix added to the dispatch without a row here fails). The token differential alone cannot see
/// a prefix dropped from the SHARED table (both sides would agree on identifier + string), so each
/// prefix also has an executed cell inside a hole, pinned to python (3.14 for <c>t</c>; <c>d</c>/<c>dr</c>/
/// <c>df</c> are PEP 822 and pinned to their dedent result). <c>rf</c>/<c>fr</c> and uppercase prefixes are
/// unsupported everywhere (#2045).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class HolePrefixMatrixTests : IntegrationTestBase
{
    public HolePrefixMatrixTests(ITestOutputHelper output) : base(output) { }

    private static readonly string[] Prefixes = { "", "f", "t", "r", "b", "d", "dr", "df" };

    private static readonly string[] Quotes = { "'", "\"", "'''", "\"\"\"" };

    [Fact]
    [Trait("Category", "Conformance")]
    public void PrefixRows_AreTheLexersPrefixTable()
    {
        Assert.Equal(Prefixes.OrderBy(p => p, StringComparer.Ordinal), SLexer.StringLiteralPrefixes.OrderBy(p => p, StringComparer.Ordinal));
    }

    public static TheoryData<string, string> PrefixQuoteCells()
    {
        var data = new TheoryData<string, string>();
        foreach (var prefix in Prefixes)
        {
            foreach (var quote in Quotes)
                data.Add(prefix, quote);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(PrefixQuoteCells))]
    [Trait("Category", "Conformance")]
    public void Literal_LexesIdenticallyInsideAHole(string prefix, string quote)
    {
        var body = prefix.Contains('f', StringComparison.Ordinal) || prefix == "t" ? "{s}"
            : prefix.Contains('r', StringComparison.Ordinal) ? "a\\d"
            : "ab";
        var literal = prefix + quote + body + quote;

        var (topTokens, topErrors) = Lex("v = " + literal + "\n");
        var top = topTokens.Skip(2).TakeWhile(t => t.Type is not (TokenType.Newline or TokenType.Eof)).ToList();

        var (holeTokens, holeErrors) = Lex("v = f\"{" + literal + "}\"\n");
        var open = holeTokens.FindIndex(t => t.Type == TokenType.FStringExprStart);
        Assert.True(open >= 0, "no hole in " + literal);
        var depth = 0;
        var close = -1;
        for (int i = open; i < holeTokens.Count && close < 0; i++)
        {
            if (holeTokens[i].Type == TokenType.FStringExprStart)
                depth++;
            else if (holeTokens[i].Type == TokenType.FStringExprEnd && --depth == 0)
                close = i;
        }
        Assert.True(close > open, $"{literal}: the hole never closed: " + Describe(holeTokens));
        var inHole = holeTokens.Skip(open + 1).Take(close - open - 1).ToList();

        Assert.Equal(topErrors, holeErrors);
        Assert.Equal(Describe(top), Describe(inHole));
    }

    /// <summary>(label, statement, python oracle). Each prefix executed inside a hole.</summary>
    public static TheoryData<string, string, string> ExecutedCells => new()
    {
        { "bare", "print(f\"{'ab'}\")", "ab" },
        { "bare.same_quote", "print(f\"{\"ab\"}\")", "ab" },
        { "f", "print(f\"{f'{s}'}\")", "ab" },
        { "t", "print(f\"{t'{s}'!r}\")", "Template(strings=('', ''), interpolations=(Interpolation('ab', 's', None, ''),))" },
        { "t.same_quote", "print(f\"{t\"{s}\"!r}\")", "Template(strings=('', ''), interpolations=(Interpolation('ab', 's', None, ''),))" },
        { "r", "print(f\"{r'\\d'}\")", "\\d" },
        { "r.same_quote_concat", "print(f\"{r\"\\d\" + 'x'}\")", "\\dx" },
        { "b", "print(f\"{b'ab'}\")", "b'ab'" },
        { "b.triple", "print(f\"{b'''ab'''}\")", "b'ab'" },
        { "d", "print(f\"{d'ab'}\")", "ab" },
        { "dr", "print(f\"{dr'a\\d'}\")", "a\\d" },
        { "df", "print(f\"{df'{s}'}\")", "ab" },
    };

    [Theory]
    [MemberData(nameof(ExecutedCells))]
    [Trait("Category", "Conformance")]
    public void PrefixedLiteralInAHole_Runs(string label, string statement, string oracle)
    {
        var result = CompileAndExecute("def main():\n    s = \"ab\"\n    " + statement + "\n", executionTimeoutMs: 15_000);
        Assert.True(result.Success, $"{label}: " + string.Join("; ", result.CompilationErrors) + result.StandardError);
        Assert.Equal(oracle, result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n'));
    }

    private static (List<SToken> Tokens, List<string?> Errors) Lex(string source)
    {
        var lexer = new SLexer(source);
        var tokens = lexer.TokenizeAll();
        return (tokens, lexer.Diagnostics.GetErrors().Select(d => d.Code).ToList());
    }

    private static string Describe(IEnumerable<SToken> tokens) =>
        string.Join(" ", tokens.Select(t => $"{t.Type}:{t.Value}"));
}
