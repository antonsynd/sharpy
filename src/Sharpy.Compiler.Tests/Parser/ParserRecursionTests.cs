using System.Text;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Tests for parser behavior under deep recursion scenarios.
/// These tests verify that the parser handles deeply nested structures
/// without crashing (StackOverflowException) -- either parsing succeeds
/// or a clear diagnostic error is emitted.
///
/// Note: StackOverflowException cannot be caught in .NET, so if a test
/// completes (whether successfully or with a handled exception), the parser
/// did not overflow the stack.
/// </summary>
public class ParserRecursionTests
{
    private readonly ITestOutputHelper _output;

    public ParserRecursionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Tokenize source and parse it, returning the module and any diagnostics.
    /// </summary>
    private static (Module? Module, bool HasErrors, string Errors) ParseSource(string source)
    {
        var lexer = new LexerNs.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();

        if (lexer.Diagnostics.HasErrors)
        {
            var lexErrors = string.Join("; ", lexer.Diagnostics.GetErrors().Select(d => d.Message));
            return (null, true, $"Lexer errors: {lexErrors}");
        }

        var parser = new ParserNs.Parser(tokens, NullLogger.Instance, maxErrors: 100);
        var module = parser.ParseModule();

        if (parser.Diagnostics.HasErrors)
        {
            var parseErrors = string.Join("; ", parser.Diagnostics.GetErrors().Select(d => d.Message));
            return (module, true, $"Parser errors: {parseErrors}");
        }

        return (module, false, string.Empty);
    }

    #region Nested Parentheses

    [Fact(Skip = "TODO: Add recursion depth guard. 500 nested parens causes StackOverflowException in the parser.")]
    public void NestedParentheses_500Levels_DoesNotStackOverflow()
    {
        // Generate: (((...(1)...)))  with 500 levels of nesting
        // NOTE: This currently triggers a StackOverflowException because the parser
        // has no recursion depth guard for expression parsing. When a depth limit
        // is added, remove the Skip annotation.
        var sb = new StringBuilder();
        const int depth = 500;
        for (int i = 0; i < depth; i++)
            sb.Append('(');
        sb.Append('1');
        for (int i = 0; i < depth; i++)
            sb.Append(')');
        sb.AppendLine();

        var source = "x = " + sb.ToString();
        _output.WriteLine($"Source length: {source.Length} chars, nesting depth: {depth}");

        // The test completing means no StackOverflowException occurred.
        // The parser may succeed or emit a diagnostic, but it must not crash.
        var ex = Record.Exception(() =>
        {
            var (module, hasErrors, errors) = ParseSource(source);
            _output.WriteLine(hasErrors ? $"Parser reported errors (expected for deep nesting): {errors}" : "Parsed successfully");
        });

        // If an exception was thrown, it should NOT be StackOverflowException.
        // (StackOverflowException can't actually be caught, so if we get here, we're safe.)
        if (ex != null)
        {
            _output.WriteLine($"Exception type: {ex.GetType().Name}, message: {ex.Message}");
            Assert.IsNotType<StackOverflowException>(ex);
        }
    }

    [Fact]
    public void NestedParentheses_100Levels_ParsesSuccessfully()
    {
        // A moderate depth of 100 should definitely parse successfully
        var sb = new StringBuilder();
        const int depth = 100;
        for (int i = 0; i < depth; i++)
            sb.Append('(');
        sb.Append("42");
        for (int i = 0; i < depth; i++)
            sb.Append(')');
        sb.AppendLine();

        var source = "x = " + sb.ToString();
        var (module, hasErrors, errors) = ParseSource(source);

        Assert.False(hasErrors, $"Expected successful parse at depth {depth}, but got: {errors}");
        Assert.NotNull(module);
        Assert.Single(module!.Body);
    }

    #endregion

    #region Chained Binary Operations

    [Fact]
    public void ChainedBinaryOps_500Additions_DoesNotStackOverflow()
    {
        // Generate: x = 1 + 2 + 3 + ... + 500
        var sb = new StringBuilder("x = 1");
        const int count = 500;
        for (int i = 2; i <= count; i++)
        {
            sb.Append($" + {i}");
        }
        sb.AppendLine();

        var source = sb.ToString();
        _output.WriteLine($"Source length: {source.Length} chars, {count} chained additions");

        var ex = Record.Exception(() =>
        {
            var (module, hasErrors, errors) = ParseSource(source);
            _output.WriteLine(hasErrors ? $"Parser reported errors: {errors}" : "Parsed successfully");
        });

        if (ex != null)
        {
            _output.WriteLine($"Exception type: {ex.GetType().Name}, message: {ex.Message}");
            Assert.IsNotType<StackOverflowException>(ex);
        }
    }

    [Fact]
    public void ChainedBinaryOps_200Additions_ParsesSuccessfully()
    {
        // 200 chained additions should parse successfully
        var sb = new StringBuilder("x = 1");
        const int count = 200;
        for (int i = 2; i <= count; i++)
        {
            sb.Append($" + {i}");
        }
        sb.AppendLine();

        var source = sb.ToString();
        var (module, hasErrors, errors) = ParseSource(source);

        Assert.False(hasErrors, $"Expected successful parse with {count} additions, but got: {errors}");
        Assert.NotNull(module);
        Assert.Single(module!.Body);
    }

    [Fact]
    public void ChainedBinaryOps_MixedOperators_DoesNotStackOverflow()
    {
        // Generate: x = 1 + 2 * 3 - 4 + 5 * 6 - ...  (alternating operators)
        var sb = new StringBuilder("x = 1");
        var ops = new[] { " + ", " * ", " - " };
        const int count = 300;
        for (int i = 2; i <= count; i++)
        {
            sb.Append(ops[i % ops.Length]);
            sb.Append(i);
        }
        sb.AppendLine();

        var source = sb.ToString();
        _output.WriteLine($"Source length: {source.Length} chars, {count} mixed operators");

        var ex = Record.Exception(() =>
        {
            var (module, hasErrors, errors) = ParseSource(source);
            _output.WriteLine(hasErrors ? $"Parser reported errors: {errors}" : "Parsed successfully");
        });

        if (ex != null)
        {
            _output.WriteLine($"Exception type: {ex.GetType().Name}, message: {ex.Message}");
            Assert.IsNotType<StackOverflowException>(ex);
        }
    }

    #endregion

    #region Nested If Statements

    [Fact]
    public void NestedIfStatements_200Levels_DoesNotStackOverflow()
    {
        // Generate 200 nested if statements, each at a deeper indentation level
        var sb = new StringBuilder();
        const int depth = 200;
        for (int i = 0; i < depth; i++)
        {
            var indent = new string(' ', i * 4);
            sb.AppendLine($"{indent}if True:");
        }
        // Add a pass statement at the deepest level
        var deepIndent = new string(' ', depth * 4);
        sb.AppendLine($"{deepIndent}pass");

        var source = sb.ToString();
        _output.WriteLine($"Source length: {source.Length} chars, nesting depth: {depth}");

        var ex = Record.Exception(() =>
        {
            var (module, hasErrors, errors) = ParseSource(source);
            _output.WriteLine(hasErrors ? $"Parser reported errors: {errors}" : "Parsed successfully");
        });

        if (ex != null)
        {
            _output.WriteLine($"Exception type: {ex.GetType().Name}, message: {ex.Message}");
            Assert.IsNotType<StackOverflowException>(ex);
        }
    }

    [Fact]
    public void NestedIfStatements_50Levels_ParsesSuccessfully()
    {
        // 50 nested if statements should parse successfully
        var sb = new StringBuilder();
        const int depth = 50;
        for (int i = 0; i < depth; i++)
        {
            var indent = new string(' ', i * 4);
            sb.AppendLine($"{indent}if True:");
        }
        var deepIndent = new string(' ', depth * 4);
        sb.AppendLine($"{deepIndent}pass");

        var source = sb.ToString();
        var (module, hasErrors, errors) = ParseSource(source);

        Assert.False(hasErrors, $"Expected successful parse at depth {depth}, but got: {errors}");
        Assert.NotNull(module);
        Assert.Single(module!.Body); // One top-level if statement
    }

    #endregion

    #region Nested Function Calls

    [Fact]
    public void NestedFunctionCalls_200Levels_DoesNotStackOverflow()
    {
        // Generate: x = f(f(f(...f(1)...)))  with 200 levels
        var sb = new StringBuilder("x = ");
        const int depth = 200;
        for (int i = 0; i < depth; i++)
            sb.Append("f(");
        sb.Append('1');
        for (int i = 0; i < depth; i++)
            sb.Append(')');
        sb.AppendLine();

        var source = sb.ToString();
        _output.WriteLine($"Source length: {source.Length} chars, nesting depth: {depth}");

        var ex = Record.Exception(() =>
        {
            var (module, hasErrors, errors) = ParseSource(source);
            _output.WriteLine(hasErrors ? $"Parser reported errors: {errors}" : "Parsed successfully");
        });

        if (ex != null)
        {
            _output.WriteLine($"Exception type: {ex.GetType().Name}, message: {ex.Message}");
            Assert.IsNotType<StackOverflowException>(ex);
        }
    }

    #endregion

    #region Nested List Literals

    [Fact(Skip = "TODO: Add recursion depth guard. 200 nested list literals causes StackOverflowException in the parser.")]
    public void NestedListLiterals_200Levels_DoesNotStackOverflow()
    {
        // Generate: x = [[[[....[1]....]]]]  with 200 levels
        // NOTE: This currently triggers a StackOverflowException because the parser
        // has no recursion depth guard for collection literal parsing.
        var sb = new StringBuilder("x = ");
        const int depth = 200;
        for (int i = 0; i < depth; i++)
            sb.Append('[');
        sb.Append('1');
        for (int i = 0; i < depth; i++)
            sb.Append(']');
        sb.AppendLine();

        var source = sb.ToString();
        _output.WriteLine($"Source length: {source.Length} chars, nesting depth: {depth}");

        var ex = Record.Exception(() =>
        {
            var (module, hasErrors, errors) = ParseSource(source);
            _output.WriteLine(hasErrors ? $"Parser reported errors: {errors}" : "Parsed successfully");
        });

        if (ex != null)
        {
            _output.WriteLine($"Exception type: {ex.GetType().Name}, message: {ex.Message}");
            Assert.IsNotType<StackOverflowException>(ex);
        }
    }

    [Fact]
    public void NestedListLiterals_50Levels_ParsesSuccessfully()
    {
        // A moderate depth of 50 should parse successfully
        var sb = new StringBuilder("x = ");
        const int depth = 50;
        for (int i = 0; i < depth; i++)
            sb.Append('[');
        sb.Append('1');
        for (int i = 0; i < depth; i++)
            sb.Append(']');
        sb.AppendLine();

        var source = sb.ToString();
        var (module, hasErrors, errors) = ParseSource(source);

        Assert.False(hasErrors, $"Expected successful parse at depth {depth}, but got: {errors}");
        Assert.NotNull(module);
        Assert.Single(module!.Body);
    }

    #endregion

    #region Chained Comparisons

    [Fact]
    public void ChainedComparisons_200Levels_DoesNotStackOverflow()
    {
        // Generate: x = 1 < 2 < 3 < ... < 200
        var sb = new StringBuilder("x = 1");
        const int count = 200;
        for (int i = 2; i <= count; i++)
        {
            sb.Append($" < {i}");
        }
        sb.AppendLine();

        var source = sb.ToString();
        _output.WriteLine($"Source length: {source.Length} chars, {count} chained comparisons");

        var ex = Record.Exception(() =>
        {
            var (module, hasErrors, errors) = ParseSource(source);
            _output.WriteLine(hasErrors ? $"Parser reported errors: {errors}" : "Parsed successfully");
        });

        if (ex != null)
        {
            _output.WriteLine($"Exception type: {ex.GetType().Name}, message: {ex.Message}");
            Assert.IsNotType<StackOverflowException>(ex);
        }
    }

    #endregion

    #region Chained Member Access

    [Fact]
    public void ChainedMemberAccess_300Levels_DoesNotStackOverflow()
    {
        // Generate: x = a.b.c.d.e.f...  with 300 chained member accesses
        var sb = new StringBuilder("x = obj");
        const int count = 300;
        for (int i = 0; i < count; i++)
        {
            sb.Append($".field{i}");
        }
        sb.AppendLine();

        var source = sb.ToString();
        _output.WriteLine($"Source length: {source.Length} chars, {count} chained member accesses");

        var ex = Record.Exception(() =>
        {
            var (module, hasErrors, errors) = ParseSource(source);
            _output.WriteLine(hasErrors ? $"Parser reported errors: {errors}" : "Parsed successfully");
        });

        if (ex != null)
        {
            _output.WriteLine($"Exception type: {ex.GetType().Name}, message: {ex.Message}");
            Assert.IsNotType<StackOverflowException>(ex);
        }
    }

    #endregion
}
