using System;
using System.Threading;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Tests that verify the parser terminates on malformed input that could
/// potentially cause infinite loops. Each test uses a timeout to ensure
/// the parser doesn't hang.
/// </summary>
public class ParserLoopTerminationTests
{
    /// <summary>
    /// Timeout for each test (2 seconds). If parsing takes longer,
    /// the parser is likely stuck in an infinite loop.
    /// </summary>
    private const int TimeoutMs = 2000;

    [Theory]
    [InlineData("foo(")]                          // Unclosed function call
    [InlineData("foo(a,")]                        // Trailing comma in args
    [InlineData("foo(a=1,")]                      // Trailing comma after kwarg
    [InlineData("foo(a, b,")]                     // Multiple args, trailing comma
    [InlineData("foo(a, b, c=1,")]                // Mix of args and kwargs
    public void Parser_UnclosedFunctionCall_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("[x for")]                        // Incomplete for comprehension
    [InlineData("[x for x")]                      // Missing 'in' keyword
    [InlineData("[x for x in")]                   // Missing iterable
    [InlineData("{x for")]                        // Set comprehension incomplete
    [InlineData("{k: v for")]                     // Dict comprehension incomplete
    public void Parser_IncompleteComprehension_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("lambda x,")]                     // Trailing comma in lambda params
    [InlineData("lambda x, y,")]                  // Multiple params, trailing comma
    [InlineData("lambda x, y, z,")]               // Three params, trailing comma
    public void Parser_MalformedLambda_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("import x,")]                     // Trailing comma in import
    [InlineData("import x, y,")]                  // Multiple imports, trailing comma
    [InlineData("import a.b.c,")]                 // Dotted import, trailing comma
    public void Parser_MalformedImport_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("from x import y,")]              // Trailing comma in from-import
    [InlineData("from x import y, z,")]           // Multiple names, trailing comma
    [InlineData("from x import a as b,")]         // With alias, trailing comma
    public void Parser_MalformedFromImport_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("def foo(x,")]                    // Trailing comma in function params
    [InlineData("def foo(x, y,")]                 // Multiple params, trailing comma
    [InlineData("def foo(x: int,")]               // Typed param, trailing comma
    [InlineData("def foo(x: int = 1,")]           // Default value, trailing comma
    public void Parser_MalformedFunctionParams_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("class Foo[T,")]                  // Trailing comma in type params
    [InlineData("class Foo[T, U,")]               // Multiple type params, trailing comma
    [InlineData("class Foo[T: IComparable,")]     // Constrained type param, trailing comma
    public void Parser_MalformedTypeParameters_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("class Foo[T: A &")]              // Incomplete constraint
    [InlineData("class Foo[T: A & B &")]          // Multiple constraints, trailing &
    public void Parser_MalformedConstraints_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("x: list[int,")]                  // Trailing comma in generic type
    [InlineData("x: dict[str, int,")]             // Dict type, trailing comma
    [InlineData("x: tuple[int, str,")]            // Tuple type, trailing comma
    public void Parser_MalformedGenericType_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("type F = (int,")]                // Function type, trailing comma
    [InlineData("type F = (int, str,")]           // Multiple params, trailing comma
    [InlineData("x: (int,")]                      // Tuple type shorthand, trailing comma
    public void Parser_MalformedFunctionType_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("class Foo(Bar,")]                // Base class, trailing comma
    [InlineData("class Foo(Bar, Baz,")]           // Multiple bases, trailing comma
    [InlineData("struct Foo(IBar,")]              // Struct interface, trailing comma
    [InlineData("interface Foo(IBar,")]           // Interface inheritance, trailing comma
    public void Parser_MalformedInheritance_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("(1,")]                           // Tuple literal, trailing comma
    [InlineData("[1,")]                           // List literal, trailing comma
    [InlineData("{1,")]                           // Set literal, trailing comma
    [InlineData("{\"a\": 1,")]                    // Dict literal, trailing comma
    public void Parser_MalformedCollectionLiteral_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("x.")]                            // Trailing dot
    [InlineData("x..")]                           // Double dot
    [InlineData("x[")]                            // Unclosed bracket
    [InlineData("x as")]                          // Incomplete type cast
    [InlineData("x to")]                          // Incomplete type coercion
    [InlineData("x?.")]                           // Null-conditional trailing dot
    [InlineData("x.y.")]                          // Member chain trailing dot
    [InlineData("x?.y?.")]                        // Null-conditional chain trailing dot
    public void Parser_PostfixEdgeCases_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("f\"{")]                          // Incomplete f-string expression
    [InlineData("f\"{x")]                         // Missing closing brace
    [InlineData("f\"{x:")]                        // Incomplete format spec
    [InlineData("f\"{x:d")]                       // Format spec without closing brace
    [InlineData("f\"{{")]                         // Escaped brace incomplete
    [InlineData("f\"{x}{")]                       // Second expression incomplete
    [InlineData("f\"hello {")]                    // Text then incomplete expression
    public void Parser_MalformedFString_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    [Theory]
    [InlineData("enum E:\n    ")]                 // Empty body after indent (whitespace only)
    [InlineData("enum E:\n    @")]                // Invalid token in enum body
    [InlineData("enum E:\n    123")]              // Number instead of identifier
    [InlineData("enum E:\n    +")]                // Operator instead of identifier
    [InlineData("enum E:\n    A\n    ")]          // Valid member then whitespace
    [InlineData("enum E:\n    A =")]              // Incomplete value assignment
    [InlineData("enum E:\n    A = +")]            // Invalid value expression
    public void Parser_MalformedEnum_DoesNotHang(string input)
    {
        AssertParserTerminatesWithTimeout(input);
    }

    private static void AssertParserTerminatesWithTimeout(string input)
    {
        var compiler = new Compiler();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(TimeoutMs));

        Exception? caughtException = null;
        try
        {
            var result = compiler.Compile(input, "test.spy", cts.Token);
            // We don't care if compilation succeeds or fails with errors,
            // only that it terminates without hanging.
        }
        catch (OperationCanceledException)
        {
            Assert.Fail($"Parser hung on malformed input (timeout after {TimeoutMs}ms): {input}");
        }
        catch (Exception ex)
        {
            // Other exceptions are fine - the parser terminated
            caughtException = ex;
        }

        // Verify we didn't get cancelled
        Assert.False(cts.IsCancellationRequested,
            $"Parser was cancelled due to timeout on input: {input}");
    }
}
