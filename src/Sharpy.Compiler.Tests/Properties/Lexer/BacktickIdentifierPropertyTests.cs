using CsCheck;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;

namespace Sharpy.Compiler.Tests.Properties.Lexer;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class BacktickIdentifierPropertyTests
{
    private readonly ITestOutputHelper _output;

    public BacktickIdentifierPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ValidBacktickContent_LexesToIdentifierWithFlag()
    {
        GenIdentifier.BacktickContent.Sample(content =>
        {
            var source = $"`{content}`\n";
            var lexer = new SharpyLexer(source);
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
                throw new Exception(
                    $"Unexpected errors for valid backtick content '{content}': {lexer.Diagnostics.GetErrors().First().Message}");

            var identToken = tokens.FirstOrDefault(t => t.Type == TokenType.Identifier);
            if (identToken == null)
                throw new Exception(
                    $"No Identifier token for backtick content: {content}");

            if (!identToken.IsBacktickEscaped)
                throw new Exception(
                    $"IsBacktickEscaped is false for: `{content}`");

            if (identToken.Value != content)
                throw new Exception(
                    $"Token value mismatch: expected '{content}', got '{identToken.Value}'");
        }, print: s => s, iter: 200);
    }

    [Fact]
    public void BacktickWithDot_ProducesDiagnosticError()
    {
        GenIdentifier.BacktickContentWithDot.Sample(content =>
        {
            var source = $"`{content}`\n";
            var lexer = new SharpyLexer(source);
            lexer.TokenizeAll();

            if (!lexer.Diagnostics.HasErrors)
                throw new Exception(
                    $"Expected DotInBacktickIdentifier error for: `{content}`");

            var hasDotError = lexer.Diagnostics.GetErrors()
                .Any(e => e.Message.Contains("dot") || e.Message.Contains("Dot") ||
                          e.Code == "SPY0025");

            if (!hasDotError)
                throw new Exception(
                    $"Expected dot-related error for: `{content}`, got: {lexer.Diagnostics.GetErrors().First().Message}");
        }, print: s => s, iter: 50);
    }

    [Fact]
    public void BacktickWithNewline_ProducesDiagnosticError()
    {
        GenIdentifier.BacktickContentWithNewline.Sample(content =>
        {
            var source = $"`{content}`\n";
            var lexer = new SharpyLexer(source);
            lexer.TokenizeAll();

            if (!lexer.Diagnostics.HasErrors)
                throw new Exception(
                    $"Expected UnterminatedBacktickIdentifier error for content with newline");

            var hasUnterminatedError = lexer.Diagnostics.GetErrors()
                .Any(e => e.Message.Contains("Unterminated") || e.Message.Contains("unterminated"));

            if (!hasUnterminatedError)
                throw new Exception(
                    $"Expected unterminated error, got: {lexer.Diagnostics.GetErrors().First().Message}");
        }, print: s => s, iter: 50);
    }

    [Fact]
    public void BacktickIdentifier_InExpressionContext_ParsesCorrectly()
    {
        GenIdentifier.BacktickContent.Sample(content =>
        {
            var source = $"x = `{content}`\n";
            var lexer = new SharpyLexer(source);
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
                throw new Exception(
                    $"Lexer errors for: x = `{content}`: {lexer.Diagnostics.GetErrors().First().Message}");

            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            var module = parser.ParseModule();

            if (module.Body.Length == 0)
                throw new Exception(
                    $"Empty module body for: x = `{content}`");
        }, print: s => s, iter: 200);
    }
}
