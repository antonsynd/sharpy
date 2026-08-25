using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Programmatic tests for definite-assignment analysis (#1559).
/// Covers bare declarations (<c>x: int</c>) and SPY0600 use-before-assign detection.
/// </summary>
[Collection("HeavyCompilation")]
public class DefiniteAssignmentTests : IntegrationTestBase
{
    public DefiniteAssignmentTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void SimpleUseBeforeAssign_ProducesSPY0600()
    {
        var source = @"
def main() -> None:
    x: int
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"));
    }

    [Fact]
    public void AllPathsAssigned_Accepted()
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
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("1");
    }

    [Fact]
    public void PartialPathAssigned_ProducesSPY0600()
    {
        var source = @"
def main() -> None:
    x: int
    if True:
        x = 1
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"));
    }

    [Fact]
    public void NestedBranches_AllPathsAssigned_Accepted()
    {
        var source = @"
def main() -> None:
    x: int
    if True:
        if True:
            x = 1
        else:
            x = 2
    else:
        x = 3
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("1");
    }

    [Fact]
    public void TupleUnpackingAssignment_Accepted()
    {
        var source = @"
def get_pair() -> tuple[int, str]:
    return (42, ""hello"")

def main() -> None:
    x: int
    y: str
    x, y = get_pair()
    print(x)
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Contain("42");
    }

    [Fact]
    public void VariableWithInitializer_NoFalsePositive()
    {
        var source = @"
def main() -> None:
    x: int = 0
    y: str = """"
    z: float = 1.5
    print(x)
    print(y)
    print(z)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "variables with initializers should never trigger SPY0600");
    }

    [Fact]
    public void MultipleBareDeclsSameFunction_IndependentTracking()
    {
        var source = @"
def main() -> None:
    x: int
    y: int
    x = 10
    print(x)
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'y'"));
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"),
            "x is assigned before use, only y should trigger SPY0600");
    }

    [Fact]
    public void ConditionRead_UseBeforeAssign_ProducesSPY0600()
    {
        var source = @"
def main() -> None:
    x: int
    if x:
        print(""truthy"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"),
            "bare-declared variable used as if/while condition must be caught by definite-assignment");
    }

    [Fact]
    public void WhileConditionRead_UseBeforeAssign_ProducesSPY0600()
    {
        var source = @"
def main() -> None:
    x: bool
    while x:
        break
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"),
            "bare-declared variable used as while condition must be caught by definite-assignment");
    }

    [Fact]
    public void LoopBodyAssignment_ConservativeAnalysis()
    {
        var source = @"
def main() -> None:
    x: int
    for i in range(3):
        x = i
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "loop body assignment is not definitely assigned after loop (loop may not execute)");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"));
    }

    [Fact]
    public void AssignedThenUsed_InSameBlock_Accepted()
    {
        var source = @"
def main() -> None:
    x: int
    x = 42
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("42");
    }
}
