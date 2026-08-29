using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Conformance matrix for callable references (#1672 D1.3).
/// Verifies that user-defined functions and registry builtins can be passed as values
/// (list elements, call arguments, return values) and that the callable-reference lowering
/// survives the per-file → project merge.
/// </summary>
[Collection("HeavyCompilation")]
public class CallableReferenceConformanceTests : IntegrationTestBase
{
    public CallableReferenceConformanceTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void UserFunction_AsCallArgument_Executes()
    {
        var source = @"
def double_it(x: int) -> int:
    return x * 2

def apply(f: (int) -> int, x: int) -> int:
    return f(x)

def main():
    print(apply(double_it, 5))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("10");
    }

    [Fact]
    public void UserFunction_AsReturnValue_Executes()
    {
        var source = @"
def add_one(x: int) -> int:
    return x + 1

def get_transform() -> (int) -> int:
    return add_one

def main():
    f: (int) -> int = get_transform()
    print(f(9))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("10");
    }

    [Fact]
    public void RegistryBuiltin_AsKeyArgument_Executes()
    {
        var source = @"
def main():
    words: list[str] = [""ccc"", ""a"", ""bb""]
    shortest: str = min(words, key=len)
    print(shortest)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("a");
    }

    [Fact]
    public void RegistryBuiltin_AsListElement_Executes()
    {
        var source = @"
def main():
    words: list[str] = [""hello"", ""world""]
    result: list[str] = sorted(words, key=len)
    print(result)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("['hello', 'world']");
    }

    /// <summary>
    /// Multi-file cell: a user function defined in lib.spy and used as a callable reference
    /// in main.spy. This exercises the MergeFrom path — the callable-reference lowering
    /// recorded in lib's per-file SemanticInfo must survive the merge into the project-level
    /// instance the emitter reads.
    /// </summary>
    [Fact]
    public void MultiFile_UserFunction_AsCallArgument_Executes()
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("CallableRefMultiFile")
            .AddSourceFile("lib.spy", @"
def double_it(x: int) -> int:
    return x * 2

def apply(f: (int) -> int, x: int) -> int:
    return f(x)
")
            .AddSourceFile("main.spy", @"
from lib import double_it, apply

def main():
    print(apply(double_it, 7))
")
            .CreateProjectFile();

        var result = helper.CompileAndExecute();
        result.Success.Should().BeTrue(
            "multi-file callable reference must compile and run; errors: " +
            string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("14");
    }

    /// <summary>
    /// Multi-file cell with a builtin reference: len used as a key= argument where the
    /// call site is in main.spy. The callable-reference lowering for len must survive
    /// the merge even though it was recorded in the per-file SemanticInfo for main.spy.
    /// </summary>
    [Fact]
    public void MultiFile_BuiltinReference_AsKeyArgument_Executes()
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("BuiltinRefMultiFile")
            .AddSourceFile("lib.spy", @"
def get_words() -> list[str]:
    return [""ccc"", ""a"", ""bb""]
")
            .AddSourceFile("main.spy", @"
from lib import get_words

def main():
    words: list[str] = get_words()
    shortest: str = min(words, key=len)
    print(shortest)
")
            .CreateProjectFile();

        var result = helper.CompileAndExecute();
        result.Success.Should().BeTrue(
            "multi-file builtin callable reference must compile and run; errors: " +
            string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("a");
    }
}
