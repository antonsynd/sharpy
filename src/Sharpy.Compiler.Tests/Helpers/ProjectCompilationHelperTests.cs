using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Sharpy.Compiler.Tests.Helpers;

/// <summary>
/// Demonstrates usage of ProjectCompilationHelper for multi-file compilation tests.
/// </summary>
[Collection("HeavyCompilation")]
public class ProjectCompilationHelperTests
{
    private readonly ITestOutputHelper _output;

    public ProjectCompilationHelperTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Helper_CompilesSingleFileProject()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("SingleFileTest")
            .AddSourceFile("main.spy", @"
def main():
    print('Hello from single file!')
")
            .CreateProjectFile();

        var result = helper.Compile();

        result.Success.Should().BeTrue();
        result.Diagnostics.GetErrors().Should().BeEmpty();
        result.OutputAssemblyPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Helper_CompilesMultiFileProject()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("MultiFileTest")
            .AddSourceFile("main.spy", @"
def main():
    result: int = 10 + 20
    print(f'Result: {result}')
")
            .AddSourceFile("math_utils.spy", @"
def add(a: int, b: int) -> int:
    return a + b

def multiply(a: int, b: int) -> int:
    return a * b
")
            .CreateProjectFile();

        var result = helper.Compile();

        result.Success.Should().BeTrue();
        result.Diagnostics.GetErrors().Should().BeEmpty();
        result.GeneratedCSharpFiles.Should().HaveCount(2);
    }

    [Fact]
    public void Helper_CompilesAndExecutesProject()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("ExecutionTest")
            .AddSourceFile("main.spy", @"
def main():
    print('Test output')
    print('Line 2')
")
            .CreateProjectFile();

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("Test output");
        result.StandardOutput.Should().Contain("Line 2");
    }

    [Fact]
    public void Helper_HandlesCompilationErrors()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("ErrorTest")
            .AddSourceFile("main.spy", @"
def main():
    x: int = 'not an int'  # Type error
    print(x)
")
            .CreateProjectFile();

        var result = helper.Compile();

        result.Success.Should().BeFalse();
        result.Diagnostics.GetErrors().Should().NotBeEmpty();
    }

    [Fact]
    public void Helper_SupportsPackageStructure()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("PackageTest")
            .AddPackage("mypackage", @"
# Package initialization
def package_init() -> str:
    return 'initialized'
")
            .AddPackageFile("mypackage", "module_a.spy", @"
def func_a() -> str:
    return 'from module A'
")
            .AddSourceFile("main.spy", @"
def main():
    print('Package test')
")
            .CreateProjectFile();

        var result = helper.Compile();

        result.Success.Should().BeTrue();
        result.GeneratedCSharpFiles.Should().HaveCount(3);
    }

    [Fact]
    public void Helper_SupportsNestedDirectories()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("NestedTest")
            .AddSourceFile("main.spy", @"
def main():
    print('Main file')
")
            .AddSourceFile("utils/string_utils.spy", @"
def double_string(s: str) -> str:
    return s + s
")
            .AddSourceFile("utils/math_utils.spy", @"
def square(x: int) -> int:
    return x * x
")
            .CreateProjectFile();

        var result = helper.Compile();

        result.Success.Should().BeTrue();
        result.GeneratedCSharpFiles.Should().HaveCount(3);
        result.GeneratedCSharpFiles.Keys.Should().Contain(k => k.Contains("utils"));
    }

    [Fact]
    public void Helper_SupportsCustomEntryPoint()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("CustomEntryTest")
            .WithEntryPoint("startup.spy")
            .AddSourceFile("startup.spy", @"
def main():
    print('Custom entry point')
")
            .AddSourceFile("helpers.spy", @"
def helper() -> str:
    return 'help'
")
            .CreateProjectFile();

        var result = helper.Compile();

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Helper_SupportsLibraryOutputType()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("MyLibrary")
            .WithOutputType("library")
            .AddSourceFile("lib.spy", @"
def public_api() -> int:
    return 42
")
            .CreateProjectFile();

        var result = helper.Compile();

        result.Success.Should().BeTrue();
        result.OutputAssemblyPath.Should().EndWith(".dll");
    }

    [Fact]
    public void Helper_AddMultipleSourceFilesAtOnce()
    {
        using var helper = new ProjectCompilationHelper(_output);

        var sourceFiles = new Dictionary<string, string>
        {
            ["main.spy"] = @"
def main():
    print('Main')
",
            ["module1.spy"] = @"
def func1() -> int:
    return 1
",
            ["module2.spy"] = @"
def func2() -> int:
    return 2
"
        };

        helper
            .WithRootNamespace("BulkAddTest")
            .AddSourceFiles(sourceFiles)
            .CreateProjectFile();

        var result = helper.Compile();

        result.Success.Should().BeTrue();
        result.GeneratedCSharpFiles.Should().HaveCount(3);
    }

    [Fact]
    public void Helper_AssertCompilationSucceededHelper()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("AssertTest")
            .AddSourceFile("main.spy", @"
def main():
    print('Success')
")
            .CreateProjectFile();

        var result = helper.Compile();

        // Should not throw
        var assertedResult = helper.AssertCompilationSucceeded(result);
        assertedResult.Should().BeSameAs(result);
    }

    [Fact]
    public void Helper_AssertCompilationFailedHelper()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("FailureTest")
            .AddSourceFile("main.spy", @"
def main():
    x: int = 'string'  # Type error
")
            .CreateProjectFile();

        var result = helper.Compile();

        // Should not throw since compilation failed as expected
        var assertedResult = helper.AssertCompilationFailed(result);
        assertedResult.Should().BeSameAs(result);
    }

    [Fact]
    public void Helper_AssertCompilationFailedWithPattern()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("PatternTest")
            .AddSourceFile("main.spy", @"
def main():
    x: int = 'string'
")
            .CreateProjectFile();

        var result = helper.Compile();

        // Should not throw since error contains expected pattern
        helper.AssertCompilationFailed(result, "Cannot assign");
    }

    [Fact]
    public void Helper_CleansUpTemporaryFiles()
    {
        string? tempDir;

        using (var helper = new ProjectCompilationHelper(_output))
        {
            tempDir = helper.TempDirectory;
            helper.AddSourceFile("main.spy", "def main(): pass");

            Directory.Exists(tempDir).Should().BeTrue();
        }

        // After disposal, temp directory should be deleted
        Directory.Exists(tempDir).Should().BeFalse();
    }

    [Fact]
    public void Helper_ProvidesAccessToTempDirectories()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper.TempDirectory.Should().NotBeNullOrEmpty();
        helper.ProjectDirectory.Should().NotBeNullOrEmpty();
        helper.SourceDirectory.Should().NotBeNullOrEmpty();

        Directory.Exists(helper.TempDirectory).Should().BeTrue();
        Directory.Exists(helper.ProjectDirectory).Should().BeTrue();
        Directory.Exists(helper.SourceDirectory).Should().BeTrue();
    }

    [Fact]
    public void Helper_TracksAddedSourceFiles()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .AddSourceFile("file1.spy", "# File 1")
            .AddSourceFile("file2.spy", "# File 2");

        helper.SourceFiles.Should().HaveCount(2);
        helper.SourceFiles.Should().Contain(f => f.EndsWith("file1.spy"));
        helper.SourceFiles.Should().Contain(f => f.EndsWith("file2.spy"));
    }

    [Fact]
    public void Helper_SupportsComplexMultiFileScenario()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("ComplexTest")
            .WithEntryPoint("app.spy")
            .AddPackage("models", @"
# Models package
")
            .AddPackageFile("models", "user.spy", @"
class User:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age
")
            .AddPackage("services", "")
            .AddPackageFile("services", "user_service.spy", @"
def get_user_count() -> int:
    return 10
")
            .AddSourceFile("app.spy", @"
def main():
    print('Complex application')
")
            .CreateProjectFile();

        var result = helper.Compile();

        result.Success.Should().BeTrue();
        result.GeneratedCSharpFiles.Should().HaveCount(5); // 2 __init__, user, user_service, app
    }
}
