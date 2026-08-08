using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

public class ProjectCompilerCodeGenTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public ProjectCompilerCodeGenTests(ITestOutputHelper output)
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

    /// <summary>
    /// Regression: codegen must use the SymbolTable's BuiltinRegistry so IsBuiltinSymbol
    /// identity checks agree with the semantic phase. A separate instance causes
    /// user-defined functions shadowing builtins to be emitted as the builtin call (#1278).
    /// </summary>
    [Fact]
    public void ProjectCompile_BuiltinShadowing_UsesUserDefinedFunction()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("Test")
            .AddSourceFile("main.spy", """
def hash(x: int) -> int:
    return x * 42

def main():
    print(hash(3))
""")
            .CreateProjectFile();
        var result = helper.CompileAndExecute();
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
        Assert.Equal("126\n", result.StandardOutput);
    }

    /// <summary>
    /// Multi-file variant: imported builtin-shadowing function must also use the user's definition.
    /// </summary>
    [Fact]
    public void ProjectCompile_ImportedBuiltinShadow_UsesUserDefinedFunction()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("TestImported")
            .AddSourceFile("lib.spy", """
def hash(x: int) -> int:
    return x * 42
""")
            .AddSourceFile("main.spy", """
from lib import hash

def main():
    print(hash(3))
""")
            .CreateProjectFile();
        var result = helper.CompileAndExecute();
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
        Assert.Equal("126\n", result.StandardOutput);
    }
}
