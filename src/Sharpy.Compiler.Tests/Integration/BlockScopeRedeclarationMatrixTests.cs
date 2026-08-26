using System.Linq;
using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Block-kind × cell matrix for the local-binding system (#1560, #1647).
/// Each row is a block kind; each column is a scoping scenario.
/// Every cell is an inline Sharpy program compiled through the full pipeline.
/// </summary>
[Collection("HeavyCompilation")]
public class BlockScopeRedeclarationMatrixTests : IntegrationTestBase
{
    public BlockScopeRedeclarationMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    // ================================================================
    // Block kinds
    // ================================================================

    public static TheoryData<string, string> SiblingRedeclareBareData()
    {
        var data = new TheoryData<string, string>();

        data.Add("if", @"
def main():
    if True:
        x = 1
        print(x)
    if True:
        x = 2
        print(x)
");

        data.Add("while", @"
def main():
    done1 = False
    while not done1:
        x = 1
        print(x)
        done1 = True
    done2 = False
    while not done2:
        x = 2
        print(x)
        done2 = True
");

        data.Add("for", @"
def main():
    for i in range(1):
        x = 1
        print(x)
    for i in range(1):
        x = 2
        print(x)
");

        data.Add("try", @"
def main():
    try:
        x = 1
        print(x)
    except Exception:
        pass
    try:
        x = 2
        print(x)
    except Exception:
        pass
");

        data.Add("except", @"
def main():
    try:
        raise ValueError(""a"")
    except ValueError:
        x = 1
        print(x)
    try:
        raise TypeError(""b"")
    except TypeError:
        x = 2
        print(x)
");

        data.Add("finally", @"
def main():
    try:
        pass
    finally:
        x = 1
        print(x)
    try:
        pass
    finally:
        x = 2
        print(x)
");

        data.Add("match-arm", @"
def main():
    match 1:
        case 1:
            x = 1
            print(x)
        case _:
            x = 2
            print(x)
    match 2:
        case 1:
            x = 3
            print(x)
        case _:
            x = 4
            print(x)
");

        return data;
    }

    [Theory]
    [MemberData(nameof(SiblingRedeclareBareData))]
    public void SiblingRedeclareBare(string blockKind, string source)
    {
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"[{blockKind}] sibling-redeclare (bare) should compile.\n{result.StandardOutput}\n{result.StandardError}");
        var lines = result.StandardOutput.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2, $"[{blockKind}] should print at least 2 values, got {lines.Length}");
    }

    // ================================================================
    // Sibling redeclare with annotated types
    // ================================================================

    public static TheoryData<string, string> SiblingRedeclareAnnotatedData()
    {
        var data = new TheoryData<string, string>();

        data.Add("if", @"
def main():
    if True:
        x: int = 1
        print(x)
    if True:
        x: str = ""hello""
        print(x)
");

        data.Add("for", @"
def main():
    for i in range(1):
        x: int = 42
        print(x)
    for i in range(1):
        x: str = ""world""
        print(x)
");

        data.Add("try", @"
def main():
    try:
        x: int = 10
        print(x)
    except Exception:
        x: str = ""err""
        print(x)
    finally:
        x: float = 3.14
        print(x)
");

        return data;
    }

    [Theory]
    [MemberData(nameof(SiblingRedeclareAnnotatedData))]
    public void SiblingRedeclareAnnotated(string blockKind, string source)
    {
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"[{blockKind}] sibling-redeclare (annotated) should compile.\n{result.StandardOutput}\n{result.StandardError}");
    }

    // ================================================================
    // Write-through: variable declared before block, assigned inside
    // ================================================================

    public static TheoryData<string, string, string> WriteThroughData()
    {
        var data = new TheoryData<string, string, string>();

        data.Add("if", @"
def main():
    x = 10
    if True:
        x = 20
    print(x)
", "20");

        data.Add("while", @"
def main():
    x = 0
    done = False
    while not done:
        x = 42
        done = True
    print(x)
", "42");

        data.Add("for", @"
def main():
    x = 0
    for i in range(3):
        x = i
    print(x)
", "2");

        data.Add("try", @"
def main():
    x = 0
    try:
        x = 99
    except Exception:
        pass
    print(x)
", "99");

        data.Add("nested-def", @"
def main():
    x = 10
    def inner():
        x = 20
    inner()
    print(x)
", "20");

        return data;
    }

    [Theory]
    [MemberData(nameof(WriteThroughData))]
    public void WriteThrough(string blockKind, string source, string expected)
    {
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"[{blockKind}] write-through should compile.\n{result.StandardOutput}\n{result.StandardError}");
        Assert.Equal(expected, result.StandardOutput.Trim());
    }

    // ================================================================
    // Outer redeclare after block
    // ================================================================

    public static TheoryData<string, string, string> OuterRedeclareAfterData()
    {
        var data = new TheoryData<string, string, string>();

        data.Add("if", @"
def main():
    if True:
        x = 1
        print(x)
    x = 99
    print(x)
", "1\n99");

        data.Add("for", @"
def main():
    for i in range(1):
        x = 42
        print(x)
    x = 100
    print(x)
", "42\n100");

        data.Add("try", @"
def main():
    try:
        x = 10
        print(x)
    except Exception:
        pass
    x = 20
    print(x)
", "10\n20");

        return data;
    }

    [Theory]
    [MemberData(nameof(OuterRedeclareAfterData))]
    public void OuterRedeclareAfter(string blockKind, string source, string expected)
    {
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"[{blockKind}] outer-redeclare-after should compile.\n{result.StandardOutput}\n{result.StandardError}");
        Assert.Equal(expected, result.StandardOutput.Trim());
    }

    // ================================================================
    // Read after block → SPY0200
    // ================================================================

    public static TheoryData<string, string> ReadAfterBlockData()
    {
        var data = new TheoryData<string, string>();

        data.Add("if", @"
def main():
    if True:
        y = 42
    print(y)
");

        data.Add("for", @"
def main():
    for i in range(1):
        y = 10
    print(y)
");

        data.Add("while", @"
def main():
    done = False
    while not done:
        y = 5
        done = True
    print(y)
");

        data.Add("try", @"
def main():
    try:
        y = 1
    except Exception:
        pass
    print(y)
");

        return data;
    }

    [Theory]
    [MemberData(nameof(ReadAfterBlockData))]
    public void ReadAfterBlock_SPY0200(string blockKind, string source)
    {
        var result = CompileAndExecute(source);
        Assert.False(result.Success, $"[{blockKind}] read-after-block should fail with SPY0200");
        Assert.True(result.RawDiagnostics.Any(d => d.Code == "SPY0200"),
            $"[{blockKind}] should report SPY0200. Got: {string.Join(", ", result.RawDiagnostics.Select(d => d.Code))}.\nErrors: {string.Join("\n", result.CompilationErrors)}");
    }

    // ================================================================
    // Lambda param shadows versioned outer local (#1647)
    // ================================================================

    [Fact]
    public void LambdaParamShadowsVersionedOuter()
    {
        var source = @"
def main():
    x = 10
    if True:
        x = 20
    f: (int) -> int = lambda x: x + 1
    print(x)
    print(f(100))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"lambda param shadowing should compile.\n{result.StandardOutput}\n{result.StandardError}");
        Assert.Equal("20\n101", result.StandardOutput.Trim());
    }

    // ================================================================
    // Comprehension target scoping
    // ================================================================

    [Fact]
    public void ComprehensionTargetDoesNotLeakToOuter()
    {
        var source = @"
def main():
    x = 99
    result: list[int] = [x for x in range(3)]
    print(x)
    print(result)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"comprehension scoping should compile.\n{result.StandardOutput}\n{result.StandardError}");
        var lines = result.StandardOutput.Trim().Split('\n');
        Assert.Equal("99", lines[0]);
    }
}
