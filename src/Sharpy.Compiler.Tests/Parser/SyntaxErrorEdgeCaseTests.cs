using System;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Tests for parser handling of syntax error cases from the fuzz test suite.
/// These are the 10 cases from SharpyFuzzer.GenerateWithSyntaxErrors.
/// </summary>
public class SyntaxErrorEdgeCaseTests
{
    private readonly ITestOutputHelper _output;
    private const int TimeoutMs = 2000;

    public SyntaxErrorEdgeCaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("def foo()\n    pass\n", "Missing colon after def")]
    [InlineData("class Foo\n    pass\n", "Missing colon after class")]
    [InlineData("def foo(x, y:\n    pass\n", "Missing closing paren")]
    [InlineData("    x = 1\n", "Unexpected dedent")]
    [InlineData("def foo():: \n    pass\n", "Double colon")]
    [InlineData("def foo():\n\ndef bar():\n    pass\n", "Missing body after colon")]
    [InlineData("if True:\n  x = 1\n", "Invalid indentation")]
    [InlineData("def = 42\n", "Keyword as identifier")]
    [InlineData("def (\n    x = \"unterminated\n    class\n", "Multiple errors")]
    [InlineData("class Foo:\nclass Bar:\n    pass\n", "Empty class body")]
    public void Parser_SyntaxErrorCase_DoesNotHang(string input, string description)
    {
        _output.WriteLine($"Testing: {description}");
        _output.WriteLine($"Input: {input.Replace("\n", "\\n")}");

        var compiler = new Compiler();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(TimeoutMs));

        try
        {
            var result = compiler.Compile(input, "test.spy", cts.Token);
            _output.WriteLine($"Result: Success={result.Success}, Errors={result.Diagnostics.ErrorCount}");
        }
        catch (OperationCanceledException)
        {
            Assert.Fail($"Parser hung on syntax error case: {description}");
        }
    }
}
