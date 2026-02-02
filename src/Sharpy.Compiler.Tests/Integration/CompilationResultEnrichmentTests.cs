using System.Linq;
using System.Threading;
using Sharpy.Compiler;
using Sharpy.Compiler.Lexer;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Tests verifying CompilationResult carries enriched artifacts (Phase 6.2):
/// SourceText, Tokens, SemanticBinding, and ImportResolver.
/// </summary>
public class CompilationResultEnrichmentTests
{
    [Fact]
    public void SuccessfulCompilation_PopulatesSourceText()
    {
        var code = @"
def main():
    x: int = 42
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.NotNull(result.SourceText);
        Assert.Equal(code, result.SourceText!.ToString());
        Assert.Equal("test.spy", result.SourceText.FilePath);
    }

    [Fact]
    public void SuccessfulCompilation_PopulatesTokens()
    {
        var code = @"
def main():
    x: int = 42
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.NotNull(result.Tokens);
        Assert.True(result.Tokens!.Count > 0);
        Assert.Equal(TokenType.Eof, result.Tokens[^1].Type);
    }

    [Fact]
    public void SuccessfulCompilation_PopulatesSemanticBinding()
    {
        var code = @"
def main():
    x: int = 42
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.NotNull(result.SemanticBinding);
    }

    [Fact]
    public void FailedCompilation_LexerError_StillPopulatesSourceTextAndTokens()
    {
        // Unterminated string should cause lexer error
        var code = "x = \"unterminated\n";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);
        Assert.NotNull(result.SourceText);
        Assert.NotNull(result.Tokens);
    }

    [Fact]
    public void FailedCompilation_SemanticError_StillPopulatesAllArtifacts()
    {
        // Type mismatch should cause semantic error
        var code = @"
def main():
    x: int = ""hello""
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);
        Assert.NotNull(result.SourceText);
        Assert.NotNull(result.Tokens);
        Assert.NotNull(result.Module);
        Assert.NotNull(result.SemanticBinding);
    }

    [Fact]
    public void Tokens_ContainExpectedTokenTypes()
    {
        var code = @"
def main():
    x: int = 42
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        var tokens = result.Tokens!;

        // Should contain def, main identifier, parens, colon, indent, x, etc.
        Assert.Contains(tokens, t => t.Type == TokenType.Def);
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "main");
        Assert.Contains(tokens, t => t.Type == TokenType.Integer && t.Value == "42");
    }

    [Fact]
    public void SourceText_LineAndColumnLookup_WorksFromResult()
    {
        var code = "x = 42\ny = 10\n";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        // Even if compilation fails, SourceText should be available for rendering
        Assert.NotNull(result.SourceText);
        Assert.Equal(1, result.SourceText!.GetLineNumber(0));    // 'x' is on line 1
        Assert.Equal(2, result.SourceText!.GetLineNumber(7));    // 'y' is on line 2
        Assert.Equal("x = 42", result.SourceText.GetLineText(1));
        Assert.Equal("y = 10", result.SourceText.GetLineText(2));
    }

    [Fact]
    public void SuccessfulCompilation_PopulatesImportResolver()
    {
        var code = @"
def main():
    x: int = 42
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.NotNull(result.ImportResolver);
    }

    [Fact]
    public void FailedCompilation_SemanticError_StillPopulatesImportResolver()
    {
        // Type mismatch should cause semantic error, but ImportResolver is created before type checking
        var code = @"
def main():
    x: int = ""hello""
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);
        Assert.NotNull(result.ImportResolver);
    }

    [Fact]
    public void FailedCompilation_LexerError_ImportResolverIsNull()
    {
        // Lexer errors happen before ImportResolver is created
        var code = "x = \"unterminated\n";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);
        Assert.Null(result.ImportResolver);
    }

    [Fact]
    public void ImportResolver_ExposesLoadedSpyModules()
    {
        var code = @"
def main():
    x: int = 42
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.NotNull(result.ImportResolver);
        // With no imports, LoadedSpyModules should be empty
        Assert.Empty(result.ImportResolver!.LoadedSpyModules);
    }

    [Fact]
    public void CancelledCompilation_StillPopulatesSourceTextAndTokens()
    {
        // A pre-cancelled token causes OperationCanceledException after lexing
        // (first ThrowIfCancellationRequested is after lexer phase)
        var code = @"
def main():
    x: int = 42
";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy", cts.Token);

        Assert.False(result.Success);
        // SourceText and Tokens should still be available since they're created before
        // the first cancellation check point
        Assert.NotNull(result.SourceText);
        Assert.Equal(code, result.SourceText!.ToString());
        Assert.NotNull(result.Tokens);
        Assert.True(result.Tokens!.Count > 0);
    }

    [Fact]
    public void CancelledCompilation_HasCancellationDiagnostic()
    {
        var code = @"
def main():
    x: int = 42
";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy", cts.Token);

        Assert.False(result.Success);
        var errors = result.Diagnostics.GetErrors().ToList();
        Assert.Contains(errors, d => d.Message.Contains("cancelled"));
    }
}
