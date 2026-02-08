using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Text;
using Xunit;
using Xunit.Abstractions;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Tests that CancellationToken is respected at the individual component level:
/// Lexer, Parser, and Compiler.Compile(). These tests use a pre-cancelled token
/// to verify that each component checks for cancellation early.
/// </summary>
public class CancellationTokenComponentTests
{
    private readonly ITestOutputHelper _output;

    public CancellationTokenComponentTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Create a pre-cancelled CancellationToken for testing.
    /// </summary>
    private static CancellationToken CreateCancelledToken()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    #region Lexer Cancellation Tests

    [Fact]
    public void Lexer_PreCancelledToken_ThrowsDuringTokenizeAll()
    {
        // A pre-cancelled token should cause TokenizeAll to throw OperationCanceledException
        var token = CreateCancelledToken();
        var source = "x: int = 42\nprint(x)\n";
        var lexer = new SharpyLexer(source, NullLogger.Instance, cancellationToken: token);

        Assert.ThrowsAny<OperationCanceledException>(() => lexer.TokenizeAll());
    }

    [Fact]
    public void Lexer_PreCancelledToken_WithSourceText_ThrowsDuringTokenizeAll()
    {
        // Same test but using the SourceText constructor
        var token = CreateCancelledToken();
        var sourceText = new SourceText("y: str = 'hello'\nprint(y)\n", "test.spy");
        var lexer = new SharpyLexer(sourceText, NullLogger.Instance, cancellationToken: token);

        Assert.ThrowsAny<OperationCanceledException>(() => lexer.TokenizeAll());
    }

    [Fact]
    public void Lexer_PreCancelledToken_LargeSource_TerminatesPromptly()
    {
        // Even with a large source, a pre-cancelled token should terminate quickly
        var token = CreateCancelledToken();
        var sb = new StringBuilder();
        for (int i = 0; i < 1000; i++)
        {
            sb.AppendLine($"var_{i}: int = {i}");
        }
        var lexer = new SharpyLexer(sb.ToString(), NullLogger.Instance, cancellationToken: token);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.ThrowsAny<OperationCanceledException>(() => lexer.TokenizeAll());
        sw.Stop();

        _output.WriteLine($"Cancelled lexing took {sw.ElapsedMilliseconds}ms");
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Cancelled lexing should terminate promptly, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Lexer_NonCancelledToken_CompletesSuccessfully()
    {
        // Sanity check: with a non-cancelled token, lexing should complete normally
        var source = "x: int = 42\nprint(x)\n";
        var lexer = new SharpyLexer(source, NullLogger.Instance, cancellationToken: CancellationToken.None);

        var tokens = lexer.TokenizeAll();

        Assert.NotEmpty(tokens);
        Assert.Equal(TokenType.Eof, tokens[^1].Type);
    }

    #endregion

    #region Parser Cancellation Tests

    [Fact]
    public void Parser_PreCancelledToken_LargeProgram_ThrowsDuringParseModule()
    {
        // The parser checks cancellation periodically (every ~1000 loop iterations).
        // A large program ensures enough iterations to trigger the cancellation check.
        var sb = new StringBuilder();
        for (int i = 0; i < 1200; i++)
        {
            sb.AppendLine($"x_{i}: int = {i}");
        }
        var lexer = new SharpyLexer(sb.ToString(), NullLogger.Instance);
        var tokens = lexer.TokenizeAll();

        var cancelledToken = CreateCancelledToken();
        var parser = new SharpyParser(tokens, NullLogger.Instance, cancellationToken: cancelledToken);

        Assert.ThrowsAny<OperationCanceledException>(() => parser.ParseModule());
    }

    [Fact]
    public void Parser_PreCancelledToken_SmallProgram_CompletesWithoutCrash()
    {
        // With a small program, the parser may complete before the cancellation
        // check fires (it checks every ~1000 iterations). This is by design:
        // we verify it does not crash, regardless of whether it throws.
        var source = "def foo(x: int) -> int:\n    return x + 1\n";
        var lexer = new SharpyLexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();

        var cancelledToken = CreateCancelledToken();
        var parser = new SharpyParser(tokens, NullLogger.Instance, cancellationToken: cancelledToken);

        // Either completes normally or throws OperationCanceledException -- both are acceptable
        var ex = Record.Exception(() => parser.ParseModule());
        if (ex != null)
        {
            Assert.IsAssignableFrom<OperationCanceledException>(ex);
        }
    }

    [Fact]
    public void Parser_PreCancelledToken_LargeTokenList_TerminatesPromptly()
    {
        // Generate a large program and tokenize it
        var sb = new StringBuilder();
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"def func_{i}(x: int) -> int:");
            sb.AppendLine($"    return x + {i}");
            sb.AppendLine();
        }
        var lexer = new SharpyLexer(sb.ToString(), NullLogger.Instance);
        var tokens = lexer.TokenizeAll();

        // Parse with a pre-cancelled token
        var cancelledToken = CreateCancelledToken();
        var parser = new SharpyParser(tokens, NullLogger.Instance, cancellationToken: cancelledToken);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.ThrowsAny<OperationCanceledException>(() => parser.ParseModule());
        sw.Stop();

        _output.WriteLine($"Cancelled parsing took {sw.ElapsedMilliseconds}ms");
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Cancelled parsing should terminate promptly, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Parser_NonCancelledToken_CompletesSuccessfully()
    {
        // Sanity check: with a non-cancelled token, parsing should complete normally
        var source = "def foo(x: int) -> int:\n    return x + 1\n";
        var lexer = new SharpyLexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();

        var parser = new SharpyParser(tokens, NullLogger.Instance, cancellationToken: CancellationToken.None);
        var module = parser.ParseModule();

        Assert.NotNull(module);
        Assert.NotEmpty(module.Body);
    }

    #endregion

    #region Compiler.Compile Cancellation Tests

    [Fact]
    public void Compile_PreCancelledToken_ReturnsCancelledDiagnostic()
    {
        // When Compiler.Compile() receives a pre-cancelled token,
        // it should return a failed result with a CompilationCancelled diagnostic
        var cancelledToken = CreateCancelledToken();
        var compiler = new Compiler();
        var source = "print(42)\n";

        var result = compiler.Compile(source, "test.spy", cancelledToken);

        Assert.False(result.Success);
        Assert.True(result.Diagnostics.HasErrors);
        Assert.Contains(result.Diagnostics.GetErrors(),
            d => d.Code == DiagnosticCodes.Infrastructure.CompilationCancelled);
    }

    [Fact]
    public void Compile_PreCancelledToken_DoesNotProduceGeneratedCode()
    {
        // A cancelled compilation should not produce generated C# code
        var cancelledToken = CreateCancelledToken();
        var compiler = new Compiler();
        var source = "def main():\n    print(42)\n";

        var result = compiler.Compile(source, "test.spy", cancelledToken);

        Assert.False(result.Success);
        Assert.Null(result.GeneratedCSharpCode);
    }

    [Fact]
    public void Compile_CancelledDuringLargeProgram_TerminatesPromptly()
    {
        // Use a token that cancels after a short delay
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        var compiler = new Compiler();

        // Generate a large program
        var sb = new StringBuilder();
        for (int f = 0; f < 500; f++)
        {
            sb.AppendLine($"def func_{f}(x: int) -> int:");
            for (int s = 0; s < 30; s++)
            {
                sb.AppendLine($"    v{s}: int = x + {s}");
            }
            sb.AppendLine($"    return v0 + {f}");
            sb.AppendLine();
        }
        sb.AppendLine("def main():");
        sb.AppendLine("    print(func_0(1))");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = compiler.Compile(sb.ToString(), "test.spy", cts.Token);
        sw.Stop();

        _output.WriteLine($"Compilation with 20ms cancellation took {sw.ElapsedMilliseconds}ms, success={result.Success}");

        // If the program compiled before cancellation fired, that is acceptable.
        // But if it was cancelled, it should have terminated reasonably promptly.
        if (!result.Success)
        {
            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"Cancelled compilation should terminate promptly, took {sw.ElapsedMilliseconds}ms");
        }
    }

    #endregion
}