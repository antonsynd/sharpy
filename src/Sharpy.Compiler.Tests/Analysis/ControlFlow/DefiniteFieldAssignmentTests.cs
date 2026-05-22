using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

[Collection("HeavyCompilation")]
public class DefiniteFieldAssignmentTests : IntegrationTestBase
{
    public DefiniteFieldAssignmentTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void StraightLineInit_AllFieldsAssigned_Succeeds()
    {
        var result = CompileAndExecute(@"
struct Point:
    x: int
    y: int
    def __init__(self):
        self.x = 1
        self.y = 2

def main():
    p = Point()
    print(p.x)
");
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void IfElseBothBranches_FieldAssigned_Succeeds()
    {
        var result = CompileAndExecute(@"
struct Point:
    x: int
    y: int
    def __init__(self, cond: bool):
        if cond:
            self.x = 1
        else:
            self.x = 2
        self.y = 0

def main():
    p = Point(True)
    print(p.x)
");
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void IfOnlyNoBranch_FieldNotDefinitelyAssigned_Errors()
    {
        var result = CompileAndExecute(@"
struct Point:
    x: int
    y: int
    def __init__(self, cond: bool):
        if cond:
            self.x = 1
        self.y = 0

def main():
    p = Point(True)
    print(p.x)
");
        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e => e.Contains("must initialize all fields"));
    }

    [Fact]
    public void NestedIfElse_AllPaths_Succeeds()
    {
        var result = CompileAndExecute(@"
struct Point:
    x: int
    def __init__(self, a: bool, b: bool):
        if a:
            if b:
                self.x = 1
            else:
                self.x = 2
        else:
            self.x = 3

def main():
    p = Point(True, False)
    print(p.x)
");
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void WhileLoop_FieldOnlyInLoop_Errors()
    {
        var result = CompileAndExecute(@"
struct Data:
    value: int
    def __init__(self, cond: bool):
        while cond:
            self.value = 1

def main():
    d = Data(True)
    print(d.value)
");
        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e => e.Contains("must initialize all fields"));
    }

    [Fact]
    public void TryOnly_FieldAssigned_Errors()
    {
        var result = CompileAndExecute(@"
struct Data:
    value: int
    def __init__(self):
        try:
            self.value = 42
        except Exception:
            pass

def main():
    d = Data()
    print(d.value)
");
        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e => e.Contains("must initialize all fields"));
    }

    [Fact]
    public void AugmentedAssignment_DoesNotCountAsInit_Errors()
    {
        var result = CompileAndExecute(@"
struct Counter:
    value: int
    def __init__(self):
        self.value += 1

def main():
    c = Counter()
    print(c.value)
");
        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e => e.Contains("must initialize all fields"));
    }

    [Fact]
    public void MultipleFields_SomeInitialized_ReportsOnlyMissing()
    {
        var result = CompileAndExecute(@"
struct Point3D:
    x: int
    y: int
    z: int
    def __init__(self):
        self.x = 1
        self.z = 3

def main():
    p = Point3D()
    print(p.x)
");
        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e => e.Contains("'y'"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("'x'"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("'z'"));
    }

    [Fact]
    public void IfElifElse_AllBranches_Succeeds()
    {
        var result = CompileAndExecute(@"
struct Value:
    n: int
    def __init__(self, mode: int):
        if mode == 0:
            self.n = 0
        elif mode == 1:
            self.n = 1
        else:
            self.n = -1

def main():
    v = Value(1)
    print(v.n)
");
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
    }
}
