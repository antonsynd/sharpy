using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests that variable redefinition (annotated re-declarations) produces correct C# code
/// with versioned local names. After #1560 the semantic phase (LocalNameAllocator) computes
/// versioned spellings; these tests verify the end-to-end behaviour through the full pipeline.
/// </summary>
[Collection("HeavyCompilation")]
public class RoslynEmitterVariableRedefinitionTests : IntegrationTestBase
{
    public RoslynEmitterVariableRedefinitionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void GenerateFunction_RedefineSameTypeVariable_GeneratesVersionedNames()
    {
        var source = @"
def main():
    x = 1
    x: int = 2
    x: int = 3
    print(x)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed:\n{result.StandardError}");
        Assert.Equal("3", result.StandardOutput.Trim());
    }

    [Fact]
    public void GenerateFunction_RedefineSameTypeWithExplicitType_GeneratesVersionedNames()
    {
        var source = @"
def main():
    x: int = 1
    x: int = 2
    print(x)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed:\n{result.StandardError}");
        Assert.Equal("2", result.StandardOutput.Trim());
    }

    [Fact]
    public void GenerateFunction_RedefineDifferentTypes_GeneratesVersionedNames()
    {
        var source = @"
def main():
    x = 1
    x: str = ""hello""
    print(x)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed:\n{result.StandardError}");
        Assert.Equal("hello", result.StandardOutput.Trim());
    }

    [Fact]
    public void GenerateFunction_RedefineDifferentTypesExplicit_GeneratesVersionedNames()
    {
        var source = @"
def main():
    x: int = 1
    x: str = ""hello""
    print(x)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed:\n{result.StandardError}");
        Assert.Equal("hello", result.StandardOutput.Trim());
    }

    [Fact]
    public void GenerateFunction_TupleUnpackingRedefinition_GeneratesVersionedNames()
    {
        var source = @"
def main():
    x, y = 1, 2
    x, y = 3, 4
    print(x)
    print(y)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed:\n{result.StandardError}");
        Assert.Equal("3\n4", result.StandardOutput.Trim().Replace("\r\n", "\n"));
    }

    [Fact]
    public void GenerateFunction_AugmentedAssignment_UsesCurrentVersion()
    {
        var source = @"
def main():
    x = 1
    x += 1
    x: int = 10
    x += 5
    print(x)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed:\n{result.StandardError}");
        Assert.Equal("15", result.StandardOutput.Trim());
    }

    [Fact]
    public void GenerateFunction_ComplexRedefinitionScenario_GeneratesCorrectVersions()
    {
        var source = @"
def main():
    x = 1
    print(x)
    x: int = 2
    print(x)
    x: str = ""hello""
    print(x)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed:\n{result.StandardError}");
        Assert.Equal("1\n2\nhello", result.StandardOutput.Trim().Replace("\r\n", "\n"));
    }

    [Fact]
    public void GenerateFunction_MultipleVariablesWithRedefinitions_GeneratesCorrectVersions()
    {
        var source = @"
def main():
    x = 1
    y = 2
    x: int = 3
    y: int = 4
    print(x + y)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed:\n{result.StandardError}");
        Assert.Equal("7", result.StandardOutput.Trim());
    }

    [Fact]
    public void GenerateFunction_UserDeclaredX1_NoCollision()
    {
        var source = @"
def main():
    x = 1
    x_1 = ""user""
    x: int = 2
    print(x)
    print(x_1)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed:\n{result.StandardError}");
        Assert.Equal("2\nuser", result.StandardOutput.Trim().Replace("\r\n", "\n"));
    }

    [Fact]
    public void GenerateFunction_UserDeclaredSameAsVersioned_SkipsCollision()
    {
        var source = @"
def main():
    x = 1
    x1 = ""user""
    x: int = 2
    print(x + 10)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed:\n{result.StandardError}");
        Assert.Equal("12", result.StandardOutput.Trim());
    }
}
