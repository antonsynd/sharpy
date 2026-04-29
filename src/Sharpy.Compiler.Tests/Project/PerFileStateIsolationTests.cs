using Sharpy.Compiler.Model;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

public class PerFileStateIsolationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public PerFileStateIsolationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private ProjectCompilationHelper CreateHelper()
    {
        _helper = new ProjectCompilationHelper(_output);
        return _helper;
    }

    public void Dispose()
    {
        _helper?.Dispose();
    }

    [Fact]
    public void TwoFiles_DiagnosticsAreIsolated()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("DiagIsolation");

        helper.AddSourceFile("good.spy", @"
def helper() -> str:
    return 'ok'
");
        helper.AddSourceFile("main.spy", @"
from good import helper

def main():
    result: str = helper()
    print(result)
");
        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();
        Assert.True(result.Success);

        var projectModel = result.ProjectModel!;
        var goodUnit = projectModel.Units.Values.First(u => u.FilePath.EndsWith("good.spy"));
        var mainUnit = projectModel.Units.Values.First(u => u.FilePath.EndsWith("main.spy"));

        Assert.False(goodUnit.HasErrors);
        Assert.False(mainUnit.HasErrors);
    }

    [Fact]
    public void PerFileSemanticInfo_ContainsOnlyThatFileData()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("SemanticInfoIsolation");

        helper.AddSourceFile("lib.spy", @"
x: int = 42
y: str = 'hello'
");
        helper.AddSourceFile("main.spy", @"
from lib import x

def main():
    z: int = x + 1
    print(z)
");
        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        var projectModel = result.ProjectModel!;
        var libUnit = projectModel.Units.Values.First(u => u.FilePath.EndsWith("lib.spy"));
        var mainUnit = projectModel.Units.Values.First(u => u.FilePath.EndsWith("main.spy"));

        Assert.NotNull(libUnit.FileSemanticInfo);
        Assert.NotNull(mainUnit.FileSemanticInfo);
        Assert.NotSame(libUnit.FileSemanticInfo, mainUnit.FileSemanticInfo);
    }

    [Fact]
    public void MergedProjectState_IsUnionOfPerFileState()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("MergedState");

        helper.AddSourceFile("a.spy", @"
val_a: int = 1
");
        helper.AddSourceFile("b.spy", @"
val_b: int = 2
");
        helper.AddSourceFile("main.spy", @"
from a import val_a
from b import val_b

def main():
    print(val_a + val_b)
");
        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        var projectSemanticInfo = result.ProjectModel!.SemanticInfo!;
        var totalPerFile = result.ProjectModel!.Units.Values
            .Where(u => u.FileSemanticInfo != null)
            .Sum(u => u.FileSemanticInfo!.ExpressionTypeCount);

        Assert.True(projectSemanticInfo.ExpressionTypeCount >= totalPerFile);
    }

    [Fact]
    public void PerFileSemanticInfo_IsIndependentAfterCreation()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("IndependentInfo");

        helper.AddSourceFile("lib.spy", @"
def foo() -> int:
    return 1
");
        helper.AddSourceFile("main.spy", @"
from lib import foo

def main():
    x: int = foo()
    print(x)
");
        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        var units = result.ProjectModel!.Units.Values.ToList();
        foreach (var unit in units)
        {
            if (unit.Phase == CompilationPhase.TypeChecked || unit.Phase == CompilationPhase.CodeGenerated)
            {
                Assert.NotNull(unit.FileSemanticInfo);
            }
        }
    }
}
