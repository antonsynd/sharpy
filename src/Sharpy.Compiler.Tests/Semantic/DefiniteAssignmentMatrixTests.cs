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

    // ========================================================================================= //
    // Block-kind × write-kind axis (#1668, #1672 DA)
    // ========================================================================================= //

    // --- except-as write kind ---

    [Fact]
    public void ExceptAs_HandlerName_IsAssigned()
    {
        var source = @"
def main() -> None:
    try:
        raise ValueError(""test"")
    except ValueError as e:
        print(e)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "except-as binding e is assigned at handler entry");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void ExceptAs_ReadAfterTry_ProducesSPY0600()
    {
        var source = @"
def main() -> None:
    e: str
    try:
        raise ValueError(""test"")
    except ValueError as e:
        pass
    print(e)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "e assigned only inside except handler is not definitely assigned after try");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600",
            "reading e after try/except must flag SPY0600");
    }

    // --- match capture write kind ---

    [Fact]
    public void MatchCapture_BindingPattern_IsAssigned()
    {
        var source = @"
def main() -> None:
    x: object = 42
    match x:
        case int(n):
            print(n)
        case _:
            pass
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "match capture n is assigned at case block entry");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void MatchCapture_AsPattern_IsAssigned()
    {
        var source = @"
def main() -> None:
    x: object = ""hello""
    match x:
        case str() as s:
            print(s)
        case _:
            pass
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "as-pattern capture s is assigned at case block entry");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    // --- for-else / while-else block kinds (#1668) ---

    [Fact]
    public void ForElse_VariableAssignedInBody_NotDefiniteAfterElse()
    {
        var source = @"
def main() -> None:
    x: int
    for i in range(3):
        x = i
    else:
        pass
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "for body always runs at least once for range(3) — x is assigned");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void WhileElse_VariableAssignedInBody_NotDefiniteAfterElse()
    {
        var source = @"
def main() -> None:
    x: int
    i: int = 0
    while i < 3:
        x = i
        i += 1
    else:
        pass
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "while body runs when condition is true — x is assigned");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void ForTarget_IsAssigned()
    {
        var source = @"
def main() -> None:
    for x in [1, 2, 3]:
        print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "for-target x is assigned at loop body entry");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    // --- with-as write kind ---

    [Fact]
    public void WithAs_BindingIsAssigned()
    {
        var source = @"
class Tracker:
    label: str
    def __init__(self, label: str):
        self.label = label
    def __enter__(self) -> Self:
        return self
    def __exit__(self) -> None:
        pass

def main() -> None:
    with Tracker(""a"") as t:
        print(t.label)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "with-as binding t is assigned at body entry");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    // --- walrus write kind ---

    [Fact]
    public void Walrus_AssignsVariable()
    {
        var source = @"
def main() -> None:
    xs: list[int] = [1, 2, 3]
    if (n := len(xs)) > 0:
        print(n)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "walrus operator assigns n before the if body");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    // --- plain assignment in various block kinds ---

    [Fact]
    public void IfBlock_AssignedInsideOnly_NotDefiniteAfter()
    {
        var source = @"
def main() -> None:
    x: int
    if True:
        x = 1
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("x assigned only in if-body is not definite after");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600");
    }

    [Fact]
    public void IfElse_AssignedInBoth_DefiniteAfter()
    {
        var source = @"
def main() -> None:
    x: int
    if True:
        x = 1
    else:
        x = 2
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "x assigned in both if and else branches is definite after");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void TryElse_AssignedInTryBody_NotDefiniteInElse()
    {
        var source = @"
def main() -> None:
    x: int
    try:
        x = 1
    except Exception:
        pass
    else:
        print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "x assigned in try body is definite in else (else only runs if try succeeded)");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void Finally_AssignedInsideTry_NotDefiniteInFinally()
    {
        var source = @"
def main() -> None:
    x: int
    try:
        x = 1
    finally:
        pass
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "x assigned in try body with no except is definite after finally");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void MatchCase_WildcardDefault_AssignedInAllArms_DefiniteAfter()
    {
        var source = @"
def main() -> None:
    x: int
    y: object = 42
    match y:
        case int():
            x = 1
        case _:
            x = 2
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "x assigned in all match arms (with wildcard default) is definite after");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }
}
