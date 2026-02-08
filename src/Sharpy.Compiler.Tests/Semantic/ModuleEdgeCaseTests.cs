using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for edge cases in the module/import system.
/// These tests use ProjectCompilationHelper for multi-file compilation scenarios.
/// </summary>
public class ModuleEdgeCaseTests
{
    private readonly ITestOutputHelper _output;

    public ModuleEdgeCaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Diamond Import Tests

    [Fact]
    public void DiamondImport_SharedDependency_NoErrors()
    {
        // Diamond pattern: A imports B and C, both B and C import D.
        // D's symbols should appear once with no duplicate symbol errors.
        //
        //   main
        //   / \
        //  b   c
        //   \ /
        //    d
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("DiamondTest")
            .AddSourceFile("main.spy", @"
from b import use_b
from c import use_c

def main():
    x: int = use_b()
    y: int = use_c()
    print(x + y)
")
            .AddSourceFile("b.spy", @"
from d import shared_func

def use_b() -> int:
    return shared_func() + 1
")
            .AddSourceFile("c.spy", @"
from d import shared_func

def use_c() -> int:
    return shared_func() + 2
")
            .AddSourceFile("d.spy", @"
def shared_func() -> int:
    return 10
")
            .CreateProjectFile();

        var result = helper.Compile();

        Assert.True(result.Success,
            "Diamond import should not cause duplicate symbol errors. Errors: " +
            string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void DiamondImport_SharedClass_NoErrors()
    {
        // Diamond pattern with a shared class type.
        // B and C both import the same class from D.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("DiamondClassTest")
            .AddSourceFile("main.spy", @"
from b import create_from_b
from c import create_from_c

def main():
    x: int = create_from_b()
    y: int = create_from_c()
    print(x + y)
")
            .AddSourceFile("b.spy", @"
from d import Shared

def create_from_b() -> int:
    s: Shared = Shared(1)
    return s.value
")
            .AddSourceFile("c.spy", @"
from d import Shared

def create_from_c() -> int:
    s: Shared = Shared(2)
    return s.value
")
            .AddSourceFile("d.spy", @"
class Shared:
    value: int

    def __init__(self, v: int):
        self.value = v
")
            .CreateProjectFile();

        var result = helper.Compile();

        Assert.True(result.Success,
            "Diamond import with shared class should succeed. Errors: " +
            string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    #endregion

    #region Duplicate Import Tests

    [Fact]
    public void DuplicateImport_SameModuleTwice_NoError()
    {
        // Importing the same symbol from the same module twice in one file
        // should not cause a duplicate error.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("DupImportTest")
            .AddSourceFile("main.spy", @"
from lib import helper_func
from lib import helper_func

def main():
    x: int = helper_func()
    print(x)
")
            .AddSourceFile("lib.spy", @"
def helper_func() -> int:
    return 42
")
            .CreateProjectFile();

        var result = helper.Compile();

        // The compiler may emit a warning about duplicate imports, but it should
        // not fail with an error.
        Assert.True(result.Success,
            "Duplicate import of same symbol should not be an error. Errors: " +
            string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void DuplicateImport_DifferentSymbolsSameModule_NoError()
    {
        // Importing different symbols from the same module in separate statements
        // should work without issues.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("DiffSymTest")
            .AddSourceFile("main.spy", @"
from lib import func_a
from lib import func_b

def main():
    x: int = func_a()
    y: int = func_b()
    print(x + y)
")
            .AddSourceFile("lib.spy", @"
def func_a() -> int:
    return 10

def func_b() -> int:
    return 20
")
            .CreateProjectFile();

        var result = helper.Compile();

        Assert.True(result.Success,
            "Importing different symbols from same module should succeed. Errors: " +
            string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    #endregion

    #region Self-Import Tests

    [Fact]
    public void SelfImport_ProducesError()
    {
        // A module importing itself should produce a clear error
        // (either circular import or module not found).
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("SelfImportTest")
            .AddSourceFile("main.spy", @"
from main import something

def something() -> int:
    return 42

def main():
    print(something())
")
            .CreateProjectFile();

        var result = helper.Compile();

        // Self-import should produce an error
        Assert.False(result.Success,
            "Self-import should produce a compilation error");

        var errorMessages = string.Join(" ", result.Diagnostics.GetErrors().Select(e => e.Message.ToLower()));
        _output.WriteLine($"Error messages: {errorMessages}");

        // The error should mention circular import or module not found
        Assert.True(
            errorMessages.Contains("circular") || errorMessages.Contains("not found") ||
            errorMessages.Contains("cannot") || errorMessages.Contains("import") ||
            errorMessages.Contains("self"),
            $"Expected error about circular or failed import, got: {errorMessages}");
    }

    #endregion

    #region Import Chain Tests

    [Fact]
    public void LongImportChain_FiveDeep_Succeeds()
    {
        // A long import chain: main -> a -> b -> c -> d -> e
        // This tests that the module resolution system handles deep chains.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("ChainTest")
            .AddSourceFile("main.spy", @"
from a import get_a

def main():
    result: int = get_a()
    print(result)
")
            .AddSourceFile("a.spy", @"
from b import get_b

def get_a() -> int:
    return get_b() + 1
")
            .AddSourceFile("b.spy", @"
from c import get_c

def get_b() -> int:
    return get_c() + 1
")
            .AddSourceFile("c.spy", @"
from d import get_d

def get_c() -> int:
    return get_d() + 1
")
            .AddSourceFile("d.spy", @"
from e import get_e

def get_d() -> int:
    return get_e() + 1
")
            .AddSourceFile("e.spy", @"
def get_e() -> int:
    return 1
")
            .CreateProjectFile();

        var result = helper.Compile();

        Assert.True(result.Success,
            "Long import chain (5 deep) should succeed. Errors: " +
            string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    #endregion

    #region Module With Multiple Exports

    [Fact]
    public void ModuleWithManyExports_AllAccessible()
    {
        // A module exporting multiple functions and classes that are all
        // imported by the main module.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("MultiExportTest")
            .AddSourceFile("main.spy", @"
from utils import add, multiply, negate

def main():
    a: int = add(1, 2)
    b: int = multiply(3, 4)
    c: int = negate(5)
    print(a + b + c)
")
            .AddSourceFile("utils.spy", @"
def add(a: int, b: int) -> int:
    return a + b

def multiply(a: int, b: int) -> int:
    return a * b

def negate(x: int) -> int:
    return 0 - x
")
            .CreateProjectFile();

        var result = helper.Compile();

        Assert.True(result.Success,
            "Multiple exports from one module should all be accessible. Errors: " +
            string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    #endregion

    #region Missing Module Import

    [Fact]
    public void ImportNonexistentModule_ProducesError()
    {
        // Importing a module that does not exist should produce a clear error.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("MissingModuleTest")
            .AddSourceFile("main.spy", @"
from nonexistent import something

def main():
    print(something())
")
            .CreateProjectFile();

        var result = helper.Compile();

        Assert.False(result.Success,
            "Importing a nonexistent module should fail");

        var errorMessages = string.Join(" ", result.Diagnostics.GetErrors().Select(e => e.Message.ToLower()));
        Assert.True(
            errorMessages.Contains("not found") || errorMessages.Contains("cannot find") ||
            errorMessages.Contains("nonexistent") || errorMessages.Contains("module") ||
            errorMessages.Contains("no module"),
            $"Expected error about missing module, got: {errorMessages}");
    }

    [Fact]
    public void ImportNonexistentSymbolFromModule_ProducesError()
    {
        // Importing a symbol that does not exist in a module should produce an error.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("MissingSymbolTest")
            .AddSourceFile("main.spy", @"
from lib import nonexistent_func

def main():
    print(nonexistent_func())
")
            .AddSourceFile("lib.spy", @"
def existing_func() -> int:
    return 42
")
            .CreateProjectFile();

        var result = helper.Compile();

        Assert.False(result.Success,
            "Importing a nonexistent symbol should fail");

        var errorMessages = string.Join(" ", result.Diagnostics.GetErrors().Select(e => e.Message.ToLower()));
        _output.WriteLine($"Error messages: {errorMessages}");
        Assert.True(
            errorMessages.Contains("not found") || errorMessages.Contains("cannot import") ||
            errorMessages.Contains("nonexistent") || errorMessages.Contains("does not export") ||
            errorMessages.Contains("undefined"),
            $"Expected error about missing symbol, got: {errorMessages}");
    }

    #endregion
}