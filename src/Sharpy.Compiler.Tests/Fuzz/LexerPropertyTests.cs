using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;

namespace Sharpy.Compiler.Tests.Fuzz;

/// <summary>
/// Property-based tests for the Sharpy lexer (Phase 2a).
/// These tests verify structural invariants of the token stream rather than
/// specific outputs, catching off-by-one bugs, position tracking errors,
/// and boundary violations across randomly generated inputs.
/// </summary>
[Trait("Category", "Property")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class LexerPropertyTests
{
    private readonly ITestOutputHelper _output;

    public LexerPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Property: Token start positions are monotonically non-decreasing.
    /// This catches off-by-one bugs in the lexer's position tracking.
    /// Note: Indent/Dedent tokens are synthetic and may share positions
    /// with adjacent tokens, so we check >= (non-decreasing) not > (strictly increasing).
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void TokenPositions_AreMonotonicallyNonDecreasing(int seed)
    {
        var fuzzer = new SharpyFuzzer(seed);
        var failures = new List<string>();

        for (int i = 0; i < 100; i++)
        {
            var source = fuzzer.GenerateValidLooking();
            try
            {
                var lexer = new LexerNs.Lexer(new Sharpy.Compiler.Text.SourceText(source, "test.spy"));
                var tokens = lexer.TokenizeAll();

                if (lexer.Diagnostics.HasErrors)
                    continue; // Skip inputs that produce lexer errors

                int previousStart = -1;
                for (int t = 0; t < tokens.Count; t++)
                {
                    var token = tokens[t];
                    var pos = token.Position;

                    // Skip synthetic tokens that have the same position as adjacent tokens
                    // (Indent/Dedent are synthetic boundary markers)
                    if (token.Type == LexerNs.TokenType.Indent ||
                        token.Type == LexerNs.TokenType.Dedent)
                        continue;

                    if (pos < previousStart)
                    {
                        failures.Add(
                            $"Seed {seed}, iter {i}, token[{t}] {token.Type} '{Escape(token.Value)}' " +
                            $"at pos {pos} < previous start {previousStart}");
                        break;
                    }
                    previousStart = pos;
                }
            }
            catch (Exception ex)
            {
                // Lexer crashes are tested elsewhere (FuzzTests); skip here
                _output.WriteLine($"Seed {seed}, iter {i}: Skipped due to {ex.GetType().Name}");
            }
        }

        if (failures.Count > 0)
        {
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Property: For non-synthetic tokens with non-empty text, the token's Value
    /// should appear at the corresponding source position.
    /// Synthetic tokens (Indent, Dedent, EOF, Newline) may have empty or special text.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void NonSyntheticTokenText_MatchesSourceAtPosition(int seed)
    {
        var fuzzer = new SharpyFuzzer(seed);
        var failures = new List<string>();

        for (int i = 0; i < 100; i++)
        {
            var source = fuzzer.GenerateValidLooking();
            try
            {
                var lexer = new LexerNs.Lexer(new Sharpy.Compiler.Text.SourceText(source, "test.spy"));
                var tokens = lexer.TokenizeAll();

                if (lexer.Diagnostics.HasErrors)
                    continue;

                foreach (var token in tokens)
                {
                    // Skip synthetic tokens and tokens with empty text
                    if (IsSyntheticToken(token) || string.IsNullOrEmpty(token.Value))
                        continue;

                    if (token.Position < 0)
                        continue; // Position tracking not available

                    var pos = token.Position;
                    var text = token.Value;

                    // For string tokens, the Value is the content between quotes,
                    // not the raw source. For f-string parts, the text may differ
                    // from the source. Skip these for this property.
                    if (token.Type == LexerNs.TokenType.String ||
                        token.Type == LexerNs.TokenType.RawString ||
                        token.Type == LexerNs.TokenType.FStringText ||
                        token.Type == LexerNs.TokenType.FStringFormatSpec)
                        continue;

                    // Verify the token text matches what's in the source
                    if (pos + text.Length <= source.Length)
                    {
                        var sourceSlice = source.Substring(pos, text.Length);
                        if (sourceSlice != text)
                        {
                            failures.Add(
                                $"Seed {seed}, iter {i}: token {token.Type} '{Escape(text)}' at pos {pos} " +
                                $"doesn't match source '{Escape(sourceSlice)}'");
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Skip crashes
            }
        }

        if (failures.Count > 0)
        {
            foreach (var f in failures.Take(10))
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Property: No token's position + length extends beyond the source string boundaries.
    /// Every token's reported position should be within [0, source.Length).
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void NoTokenText_ExtendsBeyondSourceBounds(int seed)
    {
        var fuzzer = new SharpyFuzzer(seed);
        var failures = new List<string>();

        for (int i = 0; i < 100; i++)
        {
            var source = fuzzer.GenerateValidLooking();
            try
            {
                var lexer = new LexerNs.Lexer(new Sharpy.Compiler.Text.SourceText(source, "test.spy"));
                var tokens = lexer.TokenizeAll();

                if (lexer.Diagnostics.HasErrors)
                    continue;

                foreach (var token in tokens)
                {
                    if (token.Position < 0)
                        continue; // Position tracking not available

                    // Position must be within source bounds
                    if (token.Position > source.Length)
                    {
                        failures.Add(
                            $"Seed {seed}, iter {i}: token {token.Type} position {token.Position} " +
                            $"exceeds source length {source.Length}");
                        continue;
                    }

                    // For tokens with non-empty text (excluding synthetic tokens),
                    // position + length should not exceed source length
                    if (!string.IsNullOrEmpty(token.Value) && !IsSyntheticToken(token))
                    {
                        if (token.Position + token.Length > source.Length)
                        {
                            failures.Add(
                                $"Seed {seed}, iter {i}: token {token.Type} '{Escape(token.Value)}' " +
                                $"at pos {token.Position} with length {token.Length} exceeds " +
                                $"source length {source.Length}");
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Skip crashes
            }
        }

        if (failures.Count > 0)
        {
            foreach (var f in failures.Take(10))
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Also test with stress inputs (not just valid-looking programs)
    /// to catch boundary issues with pathological input.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void StressInputs_TokenPositionsWithinBounds(int seed)
    {
        var fuzzer = new SharpyFuzzer(seed);
        var failures = new List<string>();

        for (int i = 0; i < 100; i++)
        {
            var source = fuzzer.GenerateLexerStress();
            try
            {
                var lexer = new LexerNs.Lexer(new Sharpy.Compiler.Text.SourceText(source, "test.spy"));
                var tokens = lexer.TokenizeAll();

                // Even if there are errors, positions should still be valid
                foreach (var token in tokens)
                {
                    if (token.Position < 0)
                        continue;

                    if (token.Position > source.Length)
                    {
                        failures.Add(
                            $"Seed {seed}, iter {i}: token {token.Type} position {token.Position} " +
                            $"exceeds source length {source.Length}");
                    }
                }
            }
            catch (Exception)
            {
                // Skip crashes - tested elsewhere
            }
        }

        if (failures.Count > 0)
        {
            foreach (var f in failures.Take(10))
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    private static bool IsSyntheticToken(LexerNs.Token token)
    {
        return token.Type == LexerNs.TokenType.Indent ||
               token.Type == LexerNs.TokenType.Dedent ||
               token.Type == LexerNs.TokenType.Eof ||
               token.Type == LexerNs.TokenType.Newline;
    }

    private static string Escape(string s)
    {
        if (s.Length > 50)
            s = s[..50] + "...";
        return s.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}
