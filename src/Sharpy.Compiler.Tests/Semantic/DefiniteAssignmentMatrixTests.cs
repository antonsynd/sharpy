using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// DA position x node-kind matrix (#1635): for each representative expression kind, an
/// unassigned read must produce SPY0600 and an assigned read must succeed.
/// </summary>
[Collection("HeavyCompilation")]
public class DefiniteAssignmentMatrixTests : IntegrationTestBase
{
    public DefiniteAssignmentMatrixTests(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> UnassignedReadCases => new[]
    {
        new object[] { "Identifier", "def main() -> None:\n    x: int\n    print(x)" },
        new object[] { "BinaryOp", "def main() -> None:\n    x: int\n    y: int = x + 1" },
        new object[] { "UnaryOp", "def main() -> None:\n    x: int\n    y: int = -x" },
        new object[] { "FunctionCall", "def main() -> None:\n    x: int\n    print(x)" },
        new object[] { "IndexAccess", "def main() -> None:\n    x: int\n    items: list[int] = [10, 20, 30]\n    y: int = items[x]" },
        new object[] { "MemberAccess", "def main() -> None:\n    x: str\n    y: str = x.upper()" },
        new object[] { "ConditionalExpression", "def main() -> None:\n    x: int\n    y: int = x if True else 0" },
        new object[] { "ListLiteral", "def main() -> None:\n    x: int\n    y: list[int] = [x]" },
        new object[] { "TupleLiteral", "def main() -> None:\n    x: int\n    y: tuple[int, int] = (x, 1)" },
        new object[] { "ComparisonChain", "def main() -> None:\n    x: int\n    y: bool = 0 < x < 10" },
        new object[] { "TypeCheck", "def main() -> None:\n    x: object\n    y: bool = isinstance(x, int)" },
        new object[] { "ExceptHandler", "def main() -> None:\n    x: int\n    try:\n        x = 1\n    except Exception:\n        print(x)" },
    };

    [Theory]
    [MemberData(nameof(UnassignedReadCases))]
    public void UnassignedRead_ProducesSPY0600(string kind, string source)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"{kind}: unassigned read must produce SPY0600");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == "SPY0600",
            $"{kind}: expected SPY0600 for unassigned variable");
    }

    public static IEnumerable<object[]> AssignedReadCases => new[]
    {
        new object[] { "Identifier", "def main() -> None:\n    x: int\n    x = 42\n    print(x)" },
        new object[] { "BinaryOp", "def main() -> None:\n    x: int\n    x = 1\n    y: int = x + 1" },
        new object[] { "UnaryOp", "def main() -> None:\n    x: int\n    x = 1\n    y: int = -x" },
        new object[] { "FunctionCall", "def main() -> None:\n    x: int\n    x = 42\n    print(x)" },
        new object[] { "IndexAccess", "def main() -> None:\n    x: int\n    x = 0\n    items: list[int] = [10, 20, 30]\n    y: int = items[x]" },
        new object[] { "MemberAccess", "def main() -> None:\n    x: str\n    x = \"hello\"\n    y: str = x.upper()" },
        new object[] { "ConditionalExpression", "def main() -> None:\n    x: int\n    x = 1\n    y: int = x if True else 0" },
        new object[] { "ListLiteral", "def main() -> None:\n    x: int\n    x = 1\n    y: list[int] = [x]" },
        new object[] { "TupleLiteral", "def main() -> None:\n    x: int\n    x = 1\n    y: tuple[int, int] = (x, 1)" },
        new object[] { "ComparisonChain", "def main() -> None:\n    x: int\n    x = 5\n    y: bool = 0 < x < 10" },
        new object[] { "TypeCheck", "def main() -> None:\n    x: object\n    x = 42\n    y: bool = isinstance(x, int)" },
    };

    [Theory]
    [MemberData(nameof(AssignedReadCases))]
    public void AssignedRead_Succeeds(string kind, string source)
    {
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == "SPY0600",
            $"{kind}: assigned read must not produce SPY0600");
        result.Success.Should().BeTrue($"{kind}: assigned read must compile successfully. Errors: {string.Join("; ", result.CompilationErrors)}");
    }

    [Fact]
    public void LambdaExpression_SeparateScope_DoesNotFlagOuterUnassigned()
    {
        var source = @"
def main() -> None:
    x: int
    f = lambda: 42
    x = 1
    print(f())
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == "SPY0600" && d.Message.Contains("'x'"),
            "lambda has its own scope — outer x not read inside lambda");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void ExceptHandler_AssignedInsideTry_ProducesSPY0600()
    {
        var source = @"
def main() -> None:
    x: int
    try:
        x = 1
    except Exception:
        print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "x assigned inside try body is not definitely assigned in except handler");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == "SPY0600",
            "except handler sees x as possibly unassigned");
    }

    [Fact]
    public void ExceptHandler_AssignedBeforeTry_Succeeds()
    {
        var source = @"
def main() -> None:
    x: int
    x = 1
    try:
        pass
    except Exception:
        print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == "SPY0600",
            "x assigned before try is definitely assigned in except handler");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }
}
