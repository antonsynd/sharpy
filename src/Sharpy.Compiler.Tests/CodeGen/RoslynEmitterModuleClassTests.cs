using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for module class code generation (RoslynEmitter.ModuleClass.cs).
/// Covers module-level variables as static fields, module-level functions as static methods,
/// and entry point (main function) generation.
/// </summary>
[Collection("HeavyCompilation")]
public class RoslynEmitterModuleClassTests : IntegrationTestBase
{
    public RoslynEmitterModuleClassTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ModuleLevelVariable_BecomesStaticField()
    {
        var result = CompileAndExecute(@"
MAX_SIZE: int = 100

def main():
    print(MAX_SIZE)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("100\n", result.StandardOutput);
    }

    [Fact]
    public void ModuleLevelFunction_BecomesStaticMethod()
    {
        var result = CompileAndExecute(@"
def add(a: int, b: int) -> int:
    return a + b

def main():
    print(add(3, 4))
    print(add(10, 20))
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("7\n30\n", result.StandardOutput);
    }

    [Fact]
    public void MainFunction_GeneratesEntryPoint()
    {
        var result = CompileAndExecute(@"
def main():
    print(""entry point works"")
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("entry point works\n", result.StandardOutput);
        // The generated C# should contain a Main method that serves as the entry point
        Assert.Contains("Main", result.GeneratedCSharp);
    }

    [Fact]
    public void ModuleLevelConstant_AccessibleFromMain()
    {
        var result = CompileAndExecute(@"
PI: float = 3.14159
APP_NAME: str = ""TestApp""

def main():
    print(APP_NAME)
    print(PI)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("TestApp\n3.14159\n", result.StandardOutput);
    }

    [Fact]
    public void ModuleLevelFunction_CalledFromOtherFunctions()
    {
        var result = CompileAndExecute(@"
def square(n: int) -> int:
    return n * n

def sum_of_squares(a: int, b: int) -> int:
    return square(a) + square(b)

def main():
    print(sum_of_squares(3, 4))
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("25\n", result.StandardOutput);
    }

    [Fact]
    public void ModuleClass_GeneratesPublicStaticPartialClass()
    {
        var result = CompileAndExecute(@"
def main():
    print(""ok"")
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        // Module class should be public static partial
        Assert.Contains("public static partial class", result.GeneratedCSharp);
    }
}
