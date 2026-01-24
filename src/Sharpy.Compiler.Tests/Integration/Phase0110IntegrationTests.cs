using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Helpers;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.10: Import System, Package Support, and Multi-File Compilation.
///
/// This phase introduces:
/// - Basic import statements (import module)
/// - Package initialization (__init__.spy)
/// - Project file support (.spyproj)
/// - Multi-file compilation
/// - Module resolution and discovery
/// </summary>
public class Phase0110IntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public Phase0110IntegrationTests(ITestOutputHelper output)
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

    #region Basic Import Tests

    [Fact]
    public void BasicImport_SimpleModule_Works()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("utils.spy", @"
def helper() -> str:
    return 'help'
");

        helper.AddSourceFile("main.spy", @"
import utils

def main():
    result = utils.helper()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void BasicImport_ImportFromSubdirectory_Works()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("lib/math.spy", @"
def add(x: int, y: int) -> int:
    return x + y
");

        helper.AddSourceFile("main.spy", @"
import lib.math

def main():
    result = lib.math.add(5, 3)
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void BasicImport_MultipleImports_Works()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("math_ops.spy", @"
def add(x: int, y: int) -> int:
    return x + y
");

        helper.AddSourceFile("string_ops.spy", @"
def concat(a: str, b: str) -> str:
    return a + b
");

        helper.AddSourceFile("main.spy", @"
import math_ops
import string_ops

def main():
    num = math_ops.add(5, 3)
    text = string_ops.concat('hello', 'world')
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void BasicImport_ImportVariable_Works()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("config.spy", @"
MAX_SIZE: int = 100
APP_NAME: str = 'MyApp'
");

        helper.AddSourceFile("main.spy", @"
import config

def main():
    size = config.MAX_SIZE
    name = config.APP_NAME
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void BasicImport_CircularImport_ReportsError()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("module_a.spy", @"
import module_b

def func_a() -> int:
    return module_b.func_b()
");

        helper.AddSourceFile("module_b.spy", @"
import module_a

def func_b() -> int:
    return module_a.func_a()
");

        helper.AddSourceFile("main.spy", @"
import module_a

def main():
    pass
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        // Should fail with circular import error
        Assert.False(result.Success, "Expected compilation to fail due to circular import");
        Assert.Contains(result.Errors, e => e.Contains("circular") || e.Contains("Circular"));
    }

    [Fact]
    public void BasicImport_ModuleNotFound_ReportsError()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("main.spy", @"
import nonexistent_module

def main():
    x = nonexistent_module.func()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.False(result.Success, "Expected compilation to fail due to module not found");
        Assert.Contains(result.Errors, e => e.Contains("not found", StringComparison.OrdinalIgnoreCase) || e.Contains("cannot find", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Package Initialization Tests

    [Fact]
    public void PackageInit_EmptyInit_MarksAsPackage()
    {
        var helper = CreateHelper();

        helper.AddPackage("mypackage", "");
        helper.AddPackageFile("mypackage", "module.spy", @"
def helper() -> str:
    return 'help'
");

        helper.AddSourceFile("main.spy", @"
import mypackage.module

def main():
    result = mypackage.module.helper()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void PackageInit_WithReExports_ExportsModuleMembers()
    {
        var helper = CreateHelper();

        helper.AddPackage("mypackage", @"
# Re-export from submodules
from .helpers import utility_func
from .data import DATA_VALUE
");

        helper.AddPackageFile("mypackage", "helpers.spy", @"
def utility_func() -> str:
    return 'utility'
");

        helper.AddPackageFile("mypackage", "data.spy", @"
DATA_VALUE: int = 42
");

        helper.AddSourceFile("main.spy", @"
import mypackage

def main():
    result = mypackage.utility_func()
    value = mypackage.DATA_VALUE
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void PackageInit_WithVariables_DefinesPackageLevelVariables()
    {
        var helper = CreateHelper();

        helper.AddPackage("mypackage", @"
VERSION: str = '1.0.0'
DEBUG: bool = True
");

        helper.AddSourceFile("main.spy", @"
import mypackage

def main():
    version = mypackage.VERSION
    debug = mypackage.DEBUG
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void PackageInit_WithFunctions_DefinesPackageLevelFunctions()
    {
        var helper = CreateHelper();

        helper.AddPackage("mypackage", @"
def package_function() -> str:
    return 'from package'

def helper() -> int:
    return 42
");

        helper.AddSourceFile("main.spy", @"
import mypackage

def main():
    result = mypackage.package_function()
    value = mypackage.helper()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void PackageInit_NestedPackages_Works()
    {
        var helper = CreateHelper();

        helper.AddPackage("parent", "");
        helper.AddPackage("parent/child", @"
def child_func() -> str:
    return 'child'
");

        helper.AddSourceFile("main.spy", @"
import parent.child

def main():
    result = parent.child.child_func()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void PackageInit_ImportFromPackage_Works()
    {
        var helper = CreateHelper();

        helper.AddPackage("utils", @"
def format_string(s: str) -> str:
    return f'[{s}]'
");

        helper.AddSourceFile("main.spy", @"
from utils import format_string

def main():
    result = format_string('test')
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    #endregion

    #region Project File Tests

    [Fact]
    public void ProjectFile_BasicConfiguration_CompilesSuccessfully()
    {
        var helper = CreateHelper();

        helper.WithRootNamespace("TestApp")
              .WithOutputType("exe")
              .WithEntryPoint("main.spy");

        helper.AddSourceFile("main.spy", @"
def main():
    x: int = 42
");

        helper.CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ProjectFile_LibraryOutputType_CompilesWithoutEntryPoint()
    {
        var helper = CreateHelper();

        helper.WithRootNamespace("MyLibrary")
              .WithOutputType("library");

        helper.AddSourceFile("lib.spy", @"
def library_function() -> int:
    return 42
");

        helper.CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ProjectFile_MultipleSourceFiles_CompilesAll()
    {
        var helper = CreateHelper();

        helper.WithRootNamespace("MultiFile")
              .WithEntryPoint("main.spy");

        helper.AddSourceFile("utils.spy", @"
def helper() -> str:
    return 'help'
");

        helper.AddSourceFile("config.spy", @"
VERSION: str = '1.0.0'
");

        helper.AddSourceFile("main.spy", @"
import utils
import config

def main():
    result = utils.helper()
    version = config.VERSION
");

        helper.CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ProjectFile_CustomSourceDirectory_FindsSourceFiles()
    {
        var helper = CreateHelper();

        helper.WithRootNamespace("CustomSrc")
              .WithEntryPoint("app.spy");

        helper.AddSourceFile("app.spy", @"
def main():
    x: int = 100
");

        helper.CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    #endregion

    #region Multi-File Compilation Tests

    [Fact]
    public void MultiFile_TwoFilesWithDependency_CompilesInCorrectOrder()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("dependency.spy", @"
def compute() -> int:
    return 42
");

        helper.AddSourceFile("main.spy", @"
import dependency

def main():
    result = dependency.compute()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void MultiFile_ThreeFilesChainedDependency_CompilesCorrectly()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("base.spy", @"
def base_func() -> int:
    return 1
");

        helper.AddSourceFile("middle.spy", @"
import base

def middle_func() -> int:
    return base.base_func() + 1
");

        helper.AddSourceFile("main.spy", @"
import middle

def main():
    result = middle.middle_func()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void MultiFile_SharedDependency_CompilesOnce()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("shared.spy", @"
SHARED_VALUE: int = 100
");

        helper.AddSourceFile("module_a.spy", @"
import shared

def get_value_a() -> int:
    return shared.SHARED_VALUE
");

        helper.AddSourceFile("module_b.spy", @"
import shared

def get_value_b() -> int:
    return shared.SHARED_VALUE
");

        helper.AddSourceFile("main.spy", @"
import module_a
import module_b

def main():
    a = module_a.get_value_a()
    b = module_b.get_value_b()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void MultiFile_ComplexDependencyGraph_ResolvesCorrectly()
    {
        var helper = CreateHelper();

        // Create a complex dependency graph:
        // main -> [a, b]
        // a -> [c, d]
        // b -> [d, e]
        // All should compile in correct topological order

        helper.AddSourceFile("c.spy", @"
def func_c() -> int:
    return 3
");

        helper.AddSourceFile("d.spy", @"
def func_d() -> int:
    return 4
");

        helper.AddSourceFile("e.spy", @"
def func_e() -> int:
    return 5
");

        helper.AddSourceFile("a.spy", @"
import c
import d

def func_a() -> int:
    return c.func_c() + d.func_d()
");

        helper.AddSourceFile("b.spy", @"
import d
import e

def func_b() -> int:
    return d.func_d() + e.func_e()
");

        helper.AddSourceFile("main.spy", @"
import a
import b

def main():
    result = a.func_a() + b.func_b()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void MultiFile_FunctionCallAcrossModules_TypeChecksCorrectly()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("calculator.spy", @"
def add(x: int, y: int) -> int:
    return x + y

def subtract(x: int, y: int) -> int:
    return x - y
");

        helper.AddSourceFile("main.spy", @"
import calculator

def main():
    sum = calculator.add(10, 5)
    diff = calculator.subtract(10, 5)
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void MultiFile_TypeMismatchAcrossModules_ReportsError()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("typed_module.spy", @"
def expects_int(x: int) -> int:
    return x * 2
");

        helper.AddSourceFile("main.spy", @"
import typed_module

def main():
    # Should fail: passing string to int parameter
    result = typed_module.expects_int('not an int')
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.False(result.Success, "Expected type error for string passed to int parameter");
    }

    #endregion

    #region Complex Integration Scenarios

    [Fact]
    public void ComplexScenario_PackageWithMultipleModulesAndReExports_Works()
    {
        var helper = CreateHelper();

        helper.AddPackage("math_lib", @"
# Re-export commonly used functions
from .basic import add, subtract
from .advanced import multiply, divide

VERSION: str = '1.0.0'
");

        helper.AddPackageFile("math_lib", "basic.spy", @"
def add(x: int, y: int) -> int:
    return x + y

def subtract(x: int, y: int) -> int:
    return x - y
");

        helper.AddPackageFile("math_lib", "advanced.spy", @"
def multiply(x: int, y: int) -> int:
    return x * y

def divide(x: int, y: int) -> int:
    return x // y
");

        helper.AddSourceFile("main.spy", @"
import math_lib

def main():
    sum = math_lib.add(10, 5)
    product = math_lib.multiply(10, 5)
    version = math_lib.VERSION
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ComplexScenario_NestedPackagesWithImports_Works()
    {
        var helper = CreateHelper();

        helper.AddPackage("app", "");
        helper.AddPackage("app/utils", @"
def format_text(s: str) -> str:
    return f'[{s}]'
");

        helper.AddPackage("app/data", @"
CONFIG: int = 100
");

        helper.AddSourceFile("main.spy", @"
import app.utils
import app.data

def main():
    text = app.utils.format_text('test')
    config = app.data.CONFIG
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ComplexScenario_MixedImportStyles_Works()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("module_a.spy", @"
VALUE_A: int = 10
");

        helper.AddSourceFile("module_b.spy", @"
def func_b() -> int:
    return 20
");

        helper.AddSourceFile("main.spy", @"
import module_a
from module_b import func_b

def main():
    total = module_a.VALUE_A + func_b()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ComplexScenario_ProjectWithPackagesAndModules_CompilesCorrectly()
    {
        var helper = CreateHelper();

        helper.WithRootNamespace("ComplexApp")
              .WithEntryPoint("main.spy");

        // Create a package
        helper.AddPackage("lib", @"
from .helpers import utility
");
        helper.AddPackageFile("lib", "helpers.spy", @"
def utility() -> str:
    return 'utility'
");

        // Create standalone modules
        helper.AddSourceFile("config.spy", @"
DEBUG: bool = True
");

        // Create main entry point
        helper.AddSourceFile("main.spy", @"
import lib
import config

def main():
    result = lib.utility()
    debug = config.DEBUG
");

        helper.CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ComplexScenario_LargeProjectWithManyFiles_CompilesEfficiently()
    {
        var helper = CreateHelper();

        helper.WithRootNamespace("LargeApp")
              .WithEntryPoint("main.spy");

        // Create 10 utility modules
        for (int i = 0; i < 10; i++)
        {
            helper.AddSourceFile($"util_{i}.spy", $@"
def func_{i}() -> int:
    return {i}
");
        }

        // Create main that imports all
        var imports = string.Join("\n", Enumerable.Range(0, 10).Select(i => $"import util_{i}"));
        var calls = string.Join("\n", Enumerable.Range(0, 10).Select(i => $"    val_{i} = util_{i}.func_{i}()"));

        helper.AddSourceFile("main.spy", $@"
{imports}

def main():
{calls}
");

        helper.CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ComplexScenario_PackageImportingFromParentPackage_Works()
    {
        var helper = CreateHelper();

        helper.AddPackage("parent", @"
PARENT_VALUE: int = 100
");

        helper.AddPackage("parent/child", @"
from .. import PARENT_VALUE

CHILD_VALUE: int = PARENT_VALUE + 50
");

        helper.AddSourceFile("main.spy", @"
import parent.child

def main():
    value = parent.child.CHILD_VALUE
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void EdgeCase_EmptyModule_CompilesSuccessfully()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("empty.spy", "");
        helper.AddSourceFile("main.spy", @"
import empty

def main():
    pass
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void EdgeCase_ModuleWithOnlyComments_CompilesSuccessfully()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("comments.spy", @"
# This is a comment
# Another comment
");

        helper.AddSourceFile("main.spy", @"
import comments

def main():
    pass
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact(Skip = "Requires package namespace isolation - symbols from different packages currently share the same global scope")]
    public void EdgeCase_ImportSameName_FromDifferentPackages_Works()
    {
        var helper = CreateHelper();

        helper.AddPackage("package_a", "");
        helper.AddPackageFile("package_a", "helper.spy", @"
def func() -> int:
    return 1
");

        helper.AddPackage("package_b", "");
        helper.AddPackageFile("package_b", "helper.spy", @"
def func() -> int:
    return 2
");

        helper.AddSourceFile("main.spy", @"
import package_a.helper
import package_b.helper

def main():
    val_a = package_a.helper.func()
    val_b = package_b.helper.func()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void EdgeCase_DeepNesting_Works()
    {
        var helper = CreateHelper();

        helper.AddPackage("level1", "");
        helper.AddPackage("level1/level2", "");
        helper.AddPackage("level1/level2/level3", @"
def deep_func() -> str:
    return 'deep'
");

        helper.AddSourceFile("main.spy", @"
import level1.level2.level3

def main():
    result = level1.level2.level3.deep_func()
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    #endregion

    #region From-Import Tests

    [Fact]
    public void FromImport_DirectSymbolAccess_Works()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("config.spy", @"
MAX_SIZE: int = 100
MIN_SIZE: int = 10

def get_range() -> int:
    return MAX_SIZE - MIN_SIZE
");

        helper.AddSourceFile("main.spy", @"
from config import MAX_SIZE, MIN_SIZE, get_range

def main() -> int:
    x: int = MAX_SIZE
    y: int = MIN_SIZE
    z: int = get_range()
    return x + y + z
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void FromImport_WithAlias_Works()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("config.spy", @"
MAX_VALUE: int = 100
");

        helper.AddSourceFile("main.spy", @"
from config import MAX_VALUE as MAX

def main() -> int:
    x: int = MAX
    return x
");

        helper.WithEntryPoint("main.spy");
        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    #endregion
}
