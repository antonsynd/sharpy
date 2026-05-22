using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for pattern matching code generation (RoslynEmitter.Patterns.cs).
/// Covers match statements, match expressions, wildcards, guards, type patterns,
/// binding patterns, and or-patterns.
/// </summary>
[Collection("HeavyCompilation")]
public class RoslynEmitterPatternTests : IntegrationTestBase
{
    public RoslynEmitterPatternTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void MatchStatement_LiteralPatterns_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
def main():
    value: int = 42
    match value:
        case 1:
            print(""one"")
        case 42:
            print(""forty-two"")
        case _:
            print(""other"")
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("forty-two\n", result.StandardOutput);
    }

    [Fact]
    public void MatchExpression_ReturnsCorrectValue()
    {
        var result = CompileAndExecute(@"
def describe(n: int) -> str:
    result: str = match n:
        case 1: ""one""
        case 2: ""two""
        case _: ""other""
    return result

def main():
    print(describe(1))
    print(describe(2))
    print(describe(99))
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("one\ntwo\nother\n", result.StandardOutput);
    }

    [Fact]
    public void MatchStatement_WildcardPattern_MatchesAnything()
    {
        var result = CompileAndExecute(@"
def main():
    value: int = 7
    match value:
        case _:
            print(""matched wildcard"")
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("matched wildcard\n", result.StandardOutput);
    }

    [Fact]
    public void MatchStatement_GuardPattern_FiltersCorrectly()
    {
        var result = CompileAndExecute(@"
def classify(n: int) -> str:
    result: str = ""unknown""
    match n:
        case x if x < 0:
            result = ""negative""
        case x if x == 0:
            result = ""zero""
        case x if x > 0:
            result = ""positive""
    return result

def main():
    print(classify(-5))
    print(classify(0))
    print(classify(42))
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("negative\nzero\npositive\n", result.StandardOutput);
    }

    [Fact]
    public void MatchStatement_TypePattern_MatchesByType()
    {
        var result = CompileAndExecute(@"
def check(x: object) -> str:
    match x:
        case int() as n:
            return ""int: "" + str(n)
        case str() as s:
            return ""str: "" + s
        case _:
            return ""other""

def main():
    print(check(42))
    print(check(""hello""))
    print(check(3.14))
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("int: 42\nstr: hello\nother\n", result.StandardOutput);
    }

    [Fact]
    public void MatchStatement_BindingPattern_CapturesValue()
    {
        var result = CompileAndExecute(@"
def main():
    value: int = 99
    match value:
        case x:
            print(x)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("99\n", result.StandardOutput);
    }

    [Fact]
    public void MatchStatement_OrPattern_MatchesMultipleAlternatives()
    {
        var result = CompileAndExecute(@"
def classify(x: int) -> str:
    match x:
        case 1 | 2 | 3:
            return ""small""
        case 4 | 5:
            return ""medium""
        case _:
            return ""large""

def main():
    print(classify(1))
    print(classify(2))
    print(classify(5))
    print(classify(100))
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("small\nsmall\nmedium\nlarge\n", result.StandardOutput);
    }

    [Fact]
    public void MatchStatement_MultipleCases_CorrectFallthrough()
    {
        var result = CompileAndExecute(@"
def day_type(day: str) -> str:
    match day:
        case ""Monday"":
            return ""start of week""
        case ""Friday"":
            return ""end of week""
        case ""Saturday"":
            return ""weekend""
        case ""Sunday"":
            return ""weekend""
        case _:
            return ""midweek""

def main():
    print(day_type(""Monday""))
    print(day_type(""Wednesday""))
    print(day_type(""Friday""))
    print(day_type(""Saturday""))
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("start of week\nmidweek\nend of week\nweekend\n", result.StandardOutput);
    }

    [Fact]
    public void MatchExpression_WithGuard_ProducesCorrectValue()
    {
        var result = CompileAndExecute(@"
def grade(score: int) -> str:
    return match score:
        case x if x >= 90: ""A""
        case x if x >= 80: ""B""
        case x if x >= 70: ""C""
        case _: ""F""

def main():
    print(grade(95))
    print(grade(85))
    print(grade(75))
    print(grade(50))
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("A\nB\nC\nF\n", result.StandardOutput);
    }
}
