using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

public class PerFileNameResolutionTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public PerFileNameResolutionTests(ITestOutputHelper output)
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
    public void ThreeFileProject_CompilesCorrectly()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("models.spy", @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
");
        helper.AddSourceFile("utils.spy", @"
from models import Point

def origin() -> Point:
    return Point(0, 0)
");
        helper.AddSourceFile("main.spy", @"
from utils import origin

def main():
    p = origin()
    print(p.x)
");
        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    [Fact]
    public void CrossFileFromImport_ResolvesCorrectly()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("PerFileFromImportTest");

        helper.AddSourceFile("lib.spy", @"
def add(a: int, b: int) -> int:
    return a + b
");
        helper.AddSourceFile("main.spy", @"
from lib import add

def main():
    result: int = add(3, 4)
    print(result)
");
        helper.WithEntryPoint("main.spy");
        var result = helper.CompileAndExecute();
        Assert.True(result.Success, $"Failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("7\n", result.StandardOutput);
    }

    [Fact]
    public void CrossFileInheritance_WorksAfterMerge()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("PerFileInheritTest");

        helper.AddSourceFile("base.spy", @"
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    def speak(self) -> str:
        return self.name
");
        helper.AddSourceFile("derived.spy", @"
from base import Animal

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    def speak(self) -> str:
        return self.name + ' barks'
");
        helper.AddSourceFile("main.spy", @"
from derived import Dog

def main():
    d = Dog('Rex')
    print(d.speak())
");
        helper.WithEntryPoint("main.spy");
        var result = helper.CompileAndExecute();
        Assert.True(result.Success, $"Failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Rex barks\n", result.StandardOutput);
    }

    [Fact]
    public void IncrementalCompilation_WorksWithPerFileTables()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("PerFileIncrTest");
        helper.WithIncremental();

        helper.AddSourceFile("lib.spy", @"
def greet() -> str:
    return 'hello'
");
        helper.AddSourceFile("main.spy", @"
from lib import greet

def main():
    print(greet())
");
        helper.WithEntryPoint("main.spy");

        var result1 = helper.CompileAndExecute();
        Assert.True(result1.Success, $"First compile failed: {string.Join(", ", result1.CompilationErrors)}");
        Assert.Equal("hello\n", result1.StandardOutput);

        var result2 = helper.CompileAndExecute();
        Assert.True(result2.Success, $"Incremental compile failed: {string.Join(", ", result2.CompilationErrors)}");
        Assert.Equal("hello\n", result2.StandardOutput);
    }
}
