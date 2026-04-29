using Sharpy.Compiler.Model;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

public class PerFileSemanticInfoTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public PerFileSemanticInfoTests(ITestOutputHelper output)
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
    public void TwoFileProject_EachFileGetsOwnSemanticInfo()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("lib.spy", @"
def greet(name: str) -> str:
    return 'Hello, ' + name
");
        helper.AddSourceFile("main.spy", @"
from lib import greet

def main():
    msg: str = greet('world')
    print(msg)
");
        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        var projectModel = result.ProjectModel!;
        var units = projectModel.Units.Values.ToList();

        foreach (var unit in units)
        {
            Assert.NotNull(unit.FileSemanticInfo);
        }

        var libUnit = units.First(u => u.FilePath.EndsWith("lib.spy"));
        var mainUnit = units.First(u => u.FilePath.EndsWith("main.spy"));
        Assert.NotSame(libUnit.FileSemanticInfo, mainUnit.FileSemanticInfo);
    }

    [Fact]
    public void PerFileSemanticInfo_ContainsExpressionTypesForThatFile()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("lib.spy", @"
x: int = 42
");
        helper.AddSourceFile("main.spy", @"
from lib import x

def main():
    y: str = 'hello'
    print(y)
");
        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        var projectModel = result.ProjectModel!;
        var units = projectModel.Units.Values.ToList();
        var libUnit = units.First(u => u.FilePath.EndsWith("lib.spy"));
        var mainUnit = units.First(u => u.FilePath.EndsWith("main.spy"));

        Assert.True(libUnit.FileSemanticInfo!.ExpressionTypeCount > 0);
        Assert.True(mainUnit.FileSemanticInfo!.ExpressionTypeCount > 0);
    }

    [Fact]
    public void PerFileSemanticInfo_MergedIntoProjectSemanticInfo()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("lib.spy", @"
val: int = 10
");
        helper.AddSourceFile("main.spy", @"
from lib import val

def main():
    print(val)
");
        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        var projectModel = result.ProjectModel!;
        var projectSemanticInfo = projectModel.SemanticInfo!;

        var totalPerFile = projectModel.Units.Values
            .Where(u => u.FileSemanticInfo != null)
            .Sum(u => u.FileSemanticInfo!.ExpressionTypeCount);

        Assert.True(projectSemanticInfo.ExpressionTypeCount >= totalPerFile,
            $"Project SemanticInfo ({projectSemanticInfo.ExpressionTypeCount}) should contain at least all per-file entries ({totalPerFile})");
    }

    [Fact]
    public void SingleFileProject_FileSemanticInfoIsSet()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("main.spy", @"
def main():
    x: int = 42
    print(x)
");
        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        var mainUnit = result.ProjectModel!.Units.Values.First();
        Assert.NotNull(mainUnit.FileSemanticInfo);
        Assert.True(mainUnit.FileSemanticInfo!.ExpressionTypeCount > 0);
    }
}
