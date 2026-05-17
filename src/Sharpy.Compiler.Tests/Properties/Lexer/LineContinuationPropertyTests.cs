using CsCheck;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;

namespace Sharpy.Compiler.Tests.Properties.Lexer;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class LineContinuationPropertyTests
{
    private readonly ITestOutputHelper _output;

    public LineContinuationPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ExplicitContinuation_TokenTypesMatchEquivalent()
    {
        GenLineContinuation.ExplicitContinuation.Sample(pair =>
        {
            var contTokens = GetSignificantTokenTypes(pair.WithContinuation);
            var equivTokens = GetSignificantTokenTypes(pair.Equivalent);

            AssertTokenTypesMatch(contTokens, equivTokens, pair.WithContinuation, pair.Equivalent);
        }, print: p => $"Continuation: {p.WithContinuation}\nEquivalent: {p.Equivalent}", iter: 200);
    }

    [Fact]
    public void ImplicitContinuationParens_TokenTypesMatchEquivalent()
    {
        GenLineContinuation.ImplicitContinuationParens.Sample(pair =>
        {
            var contTokens = GetSignificantTokenTypes(pair.WithContinuation);
            var equivTokens = GetSignificantTokenTypes(pair.Equivalent);

            AssertTokenTypesMatch(contTokens, equivTokens, pair.WithContinuation, pair.Equivalent);
        }, print: p => $"Continuation: {p.WithContinuation}\nEquivalent: {p.Equivalent}", iter: 200);
    }

    [Fact]
    public void ImplicitContinuationBrackets_TokenTypesMatchEquivalent()
    {
        GenLineContinuation.ImplicitContinuationBrackets.Sample(pair =>
        {
            var contTokens = GetSignificantTokenTypes(pair.WithContinuation);
            var equivTokens = GetSignificantTokenTypes(pair.Equivalent);

            AssertTokenTypesMatch(contTokens, equivTokens, pair.WithContinuation, pair.Equivalent);
        }, print: p => $"Continuation: {p.WithContinuation}\nEquivalent: {p.Equivalent}", iter: 200);
    }

    [Fact]
    public void ImplicitContinuationBraces_TokenTypesMatchEquivalent()
    {
        GenLineContinuation.ImplicitContinuationBraces.Sample(pair =>
        {
            var contTokens = GetSignificantTokenTypes(pair.WithContinuation);
            var equivTokens = GetSignificantTokenTypes(pair.Equivalent);

            AssertTokenTypesMatch(contTokens, equivTokens, pair.WithContinuation, pair.Equivalent);
        }, print: p => $"Continuation: {p.WithContinuation}\nEquivalent: {p.Equivalent}", iter: 200);
    }

    [Fact]
    public void BackslashWithTrailingWhitespace_ProducesError()
    {
        GenLineContinuation.BackslashWithTrailingWhitespace.Sample(source =>
        {
            var lexer = new SharpyLexer(source);
            lexer.TokenizeAll();

            if (!lexer.Diagnostics.HasErrors)
                throw new Exception(
                    $"Expected BackslashTrailingWhitespace error for:\n{source}");

            var hasTrailingWsError = lexer.Diagnostics.GetErrors()
                .Any(e => e.Message.Contains("trailing whitespace") ||
                          e.Message.Contains("Trailing whitespace"));

            if (!hasTrailingWsError)
                throw new Exception(
                    $"Expected trailing whitespace error, got: {lexer.Diagnostics.GetErrors().First().Message}\nSource: {source}");
        }, print: s => s, iter: 100);
    }

    [Fact]
    public void MixedContinuation_TokenTypesMatchEquivalent()
    {
        GenLineContinuation.MixedContinuation.Sample(pair =>
        {
            var contTokens = GetSignificantTokenTypes(pair.WithContinuation);
            var equivTokens = GetSignificantTokenTypes(pair.Equivalent);

            AssertTokenTypesMatch(contTokens, equivTokens, pair.WithContinuation, pair.Equivalent);
        }, print: p => $"Continuation: {p.WithContinuation}\nEquivalent: {p.Equivalent}", iter: 200);
    }

    private static List<TokenType> GetSignificantTokenTypes(string source)
    {
        var lexer = new SharpyLexer(source);
        var tokens = lexer.TokenizeAll();

        return tokens
            .Where(t => t.Type != TokenType.Newline &&
                        t.Type != TokenType.Indent &&
                        t.Type != TokenType.Dedent &&
                        t.Type != TokenType.Eof)
            .Select(t => t.Type)
            .ToList();
    }

    private static void AssertTokenTypesMatch(
        List<TokenType> actual, List<TokenType> expected,
        string actualSource, string expectedSource)
    {
        if (actual.Count != expected.Count)
            throw new Exception(
                $"Token count mismatch: {actual.Count} vs {expected.Count}\n" +
                $"Continuation: {actualSource}\n" +
                $"Equivalent: {expectedSource}\n" +
                $"Actual types: [{string.Join(", ", actual)}]\n" +
                $"Expected types: [{string.Join(", ", expected)}]");

        for (int i = 0; i < actual.Count; i++)
        {
            if (actual[i] != expected[i])
                throw new Exception(
                    $"Token type mismatch at index {i}: {actual[i]} vs {expected[i]}\n" +
                    $"Continuation: {actualSource}\n" +
                    $"Equivalent: {expectedSource}");
        }
    }
}
