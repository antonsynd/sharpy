using Sharpy.Compiler.Project;
using Sharpy.Compiler.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for cross-module namespace resolution in code generation.
/// Verifies that types imported from other modules have the correct
/// fully qualified names and using directives in generated C#.
/// </summary>
public class CrossModuleNamespaceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ITestOutputHelper _output;

    public CrossModuleNamespaceTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void SimpleImport_GeneratesCorrectNamespace()
    {
        // Arrange: Model class in models.spy
        var modelsPath = Path.Combine(_tempDir, "models.spy");
        File.WriteAllText(modelsPath, @"
class Product:
    name: str
");

        // Main imports from models
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from models import Product

def main() -> None:
    p = Product()
    p.name = ""Test""
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { modelsPath, mainPath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        // Check generated C# for main.spy
        Assert.NotNull(result.GeneratedCSharpFiles);
        var mainCs = result.GeneratedCSharpFiles!.GetValueOrDefault("main.cs");

        _output.WriteLine("Generated C# for main.spy:");
        foreach (var (key, value) in result.GeneratedCSharpFiles!)
        {
            _output.WriteLine($"--- {key} ---");
            _output.WriteLine(value);
        }

        // Verify: the type is a sibling in its module's namespace TestProject.Models (#2039), not
        // TestProject.Main.Models
        Assert.NotNull(mainCs);
        Assert.Contains("global::TestProject.Models.Product", mainCs);
    }

    [Fact]
    public void NestedModuleImport_GeneratesCorrectNamespace()
    {
        // Arrange: Create nested directory structure
        var libDir = Path.Combine(_tempDir, "lib");
        Directory.CreateDirectory(libDir);

        // lib/__init__.spy (package marker)
        var libInitPath = Path.Combine(libDir, "__init__.spy");
        File.WriteAllText(libInitPath, "");

        // lib/math.spy with Calculator class
        var mathPath = Path.Combine(libDir, "math.spy");
        File.WriteAllText(mathPath, @"
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b
");

        // Main imports from lib.math
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from lib.math import Calculator

def main() -> None:
    calc = Calculator()
    result = calc.add(1, 2)
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { libInitPath, mathPath, mainPath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        _output.WriteLine("Generated C# files:");
        if (result.GeneratedCSharpFiles != null)
        {
            foreach (var (key, value) in result.GeneratedCSharpFiles)
            {
                _output.WriteLine($"--- {key} ---");
                _output.WriteLine(value);
            }
        }
        _output.WriteLine($"Errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        // Check generated C# for main.spy
        Assert.NotNull(result.GeneratedCSharpFiles);
        var mainCs = result.GeneratedCSharpFiles!.GetValueOrDefault("main.cs");

        // Verify: the type is a sibling in its module's namespace TestProject.Lib.Math (#2039)
        Assert.NotNull(mainCs);
        Assert.Contains("global::TestProject.Lib.Math.Calculator", mainCs);
    }

    [Fact]
    public void ReExportedType_UsesOriginalDefiningModule()
    {
        // Arrange: Create package with re-export
        var pkgDir = Path.Combine(_tempDir, "mypackage");
        Directory.CreateDirectory(pkgDir);

        // mypackage/impl.spy - actual implementation
        var implPath = Path.Combine(pkgDir, "impl.spy");
        File.WriteAllText(implPath, @"
class SomeClass:
    value: int
");

        // mypackage/__init__.spy - re-exports SomeClass
        var initPath = Path.Combine(pkgDir, "__init__.spy");
        File.WriteAllText(initPath, @"
from .impl import SomeClass
");

        // Main imports from package (gets re-exported class)
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from mypackage import SomeClass

def main() -> None:
    obj = SomeClass()
    obj.value = 42
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { implPath, initPath, mainPath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        _output.WriteLine("Generated C# files:");
        if (result.GeneratedCSharpFiles != null)
        {
            foreach (var (key, value) in result.GeneratedCSharpFiles)
            {
                _output.WriteLine($"--- {key} ---");
                _output.WriteLine(value);
            }
        }
        _output.WriteLine($"Errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        // Check generated C# for main.spy
        Assert.NotNull(result.GeneratedCSharpFiles);
        var mainCs = result.GeneratedCSharpFiles!.GetValueOrDefault("main.cs");

        // Verify: the type is spelled from the module that DEFINES it, TestProject.Mypackage.Impl
        // (#2039), not from the re-exporting package's namespace TestProject.Mypackage
        Assert.NotNull(mainCs);
        Assert.Contains("global::TestProject.Mypackage.Impl.SomeClass", mainCs);
        Assert.DoesNotContain("global::TestProject.Mypackage.SomeClass", mainCs);
    }
}
