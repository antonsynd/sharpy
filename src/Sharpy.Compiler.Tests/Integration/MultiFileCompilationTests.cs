using Sharpy.Compiler;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for multi-file compilation support.
/// Tests that importing modules from separate .spy files works correctly.
/// </summary>
public class MultiFileCompilationTests : IDisposable
{
    private readonly string _tempDir;

    public MultiFileCompilationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Compile_WithImportedModule_GeneratesCSharpForBothFiles()
    {
        // Arrange
        var mainPath = Path.Combine(_tempDir, "main.spy");
        var helpersPath = Path.Combine(_tempDir, "helpers.spy");

        File.WriteAllText(mainPath, @"
from helpers import greet

def main():
    print(greet(""World""))
");

        File.WriteAllText(helpersPath, @"
def greet(name: str) -> str:
    return f""Hello, {name}!""
");

        var compiler = new Compiler();

        // Act
        var result = compiler.Compile(File.ReadAllText(mainPath), mainPath);

        // Assert
        Assert.True(result.Success, string.Join(", ", result.Errors));
        Assert.True(result.GeneratedCSharpFiles.Count >= 2,
            $"Expected at least 2 files, got {result.GeneratedCSharpFiles.Count}");
        Assert.Contains(result.GeneratedCSharpFiles.Keys,
            k => k.Contains("main", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.GeneratedCSharpFiles.Keys,
            k => k.Contains("helpers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compile_WithSingleFile_GeneratesSingleCSharpFile()
    {
        // Arrange
        var mainPath = Path.Combine(_tempDir, "single.spy");

        File.WriteAllText(mainPath, @"
def main():
    print(""Hello, World!"")
");

        var compiler = new Compiler();

        // Act
        var result = compiler.Compile(File.ReadAllText(mainPath), mainPath);

        // Assert
        Assert.True(result.Success, string.Join(", ", result.Errors));
        Assert.Single(result.GeneratedCSharpFiles);
        Assert.Contains(result.GeneratedCSharpFiles.Keys,
            k => k.Contains("single", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compile_WithMultipleImports_GeneratesCSharpForAllFiles()
    {
        // Arrange
        var mainPath = Path.Combine(_tempDir, "main.spy");
        var mathPath = Path.Combine(_tempDir, "math_utils.spy");
        var stringPath = Path.Combine(_tempDir, "string_utils.spy");

        File.WriteAllText(mainPath, @"
from math_utils import add
from string_utils import greet

def main():
    result = add(1, 2)
    message = greet(""Test"")
    print(f""{message}: {result}"")
");

        File.WriteAllText(mathPath, @"
def add(a: int, b: int) -> int:
    return a + b
");

        File.WriteAllText(stringPath, @"
def greet(name: str) -> str:
    return f""Hello, {name}!""
");

        var compiler = new Compiler();

        // Act
        var result = compiler.Compile(File.ReadAllText(mainPath), mainPath);

        // Assert
        Assert.True(result.Success, string.Join(", ", result.Errors));
        Assert.True(result.GeneratedCSharpFiles.Count >= 3,
            $"Expected at least 3 files, got {result.GeneratedCSharpFiles.Count}");
    }

    [Fact]
    public void Compile_ImportedModuleCSharp_DoesNotContainMainMethod()
    {
        // Arrange
        var mainPath = Path.Combine(_tempDir, "main.spy");
        var helperPath = Path.Combine(_tempDir, "helper.spy");

        File.WriteAllText(mainPath, @"
from helper import add

def main():
    print(add(1, 2))
");

        File.WriteAllText(helperPath, @"
def add(a: int, b: int) -> int:
    return a + b
");

        var compiler = new Compiler();

        // Act
        var result = compiler.Compile(File.ReadAllText(mainPath), mainPath);

        // Assert
        Assert.True(result.Success, string.Join(", ", result.Errors));

        // Find the helper's generated C#
        var helperCsEntry = result.GeneratedCSharpFiles.FirstOrDefault(
            kvp => kvp.Key.Contains("helper", StringComparison.OrdinalIgnoreCase));

        Assert.False(string.IsNullOrEmpty(helperCsEntry.Value), "Helper module C# code should be generated");

        // Imported modules should NOT contain Main method (only entry point has Main)
        Assert.DoesNotContain("static void Main(", helperCsEntry.Value);
    }

    [Fact]
    public void Compile_EntryFileCSharp_ContainsMainMethod()
    {
        // Arrange
        var mainPath = Path.Combine(_tempDir, "main.spy");
        var helperPath = Path.Combine(_tempDir, "helper.spy");

        File.WriteAllText(mainPath, @"
from helper import add

def main():
    print(add(1, 2))
");

        File.WriteAllText(helperPath, @"
def add(a: int, b: int) -> int:
    return a + b
");

        var compiler = new Compiler();

        // Act
        var result = compiler.Compile(File.ReadAllText(mainPath), mainPath);

        // Assert
        Assert.True(result.Success, string.Join(", ", result.Errors));

        // Find the main file's generated C#
        var mainCsEntry = result.GeneratedCSharpFiles.FirstOrDefault(
            kvp => kvp.Key.Contains("main", StringComparison.OrdinalIgnoreCase));

        Assert.False(string.IsNullOrEmpty(mainCsEntry.Value), "Main file C# code should be generated");

        // Entry point should contain Main method
        Assert.Contains("static void Main(", mainCsEntry.Value);
    }

    [Fact]
    public void Compile_WithTransitiveImports_GeneratesCSharpForAllFiles()
    {
        // Arrange: A imports B, B imports C
        var mainPath = Path.Combine(_tempDir, "main.spy");
        var utilsPath = Path.Combine(_tempDir, "utils.spy");
        var helpersPath = Path.Combine(_tempDir, "helpers.spy");

        File.WriteAllText(mainPath, @"
from utils import format_greeting

def main():
    print(format_greeting(""World""))
");

        File.WriteAllText(utilsPath, @"
from helpers import greet

def format_greeting(name: str) -> str:
    greeting = greet(name)
    return f""[FORMATTED] {greeting}""
");

        File.WriteAllText(helpersPath, @"
def greet(name: str) -> str:
    return f""Hello, {name}!""
");

        var compiler = new Compiler();

        // Act
        var result = compiler.Compile(File.ReadAllText(mainPath), mainPath);

        // Assert
        Assert.True(result.Success, string.Join(", ", result.Errors));

        // All three files should be compiled
        Assert.True(result.GeneratedCSharpFiles.Count >= 3,
            $"Expected at least 3 files for transitive imports, got {result.GeneratedCSharpFiles.Count}. " +
            $"Files: {string.Join(", ", result.GeneratedCSharpFiles.Keys.Select(Path.GetFileName))}");

        Assert.Contains(result.GeneratedCSharpFiles.Keys,
            k => k.Contains("main", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.GeneratedCSharpFiles.Keys,
            k => k.Contains("utils", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.GeneratedCSharpFiles.Keys,
            k => k.Contains("helpers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compile_WithCircularImport_ReturnsError()
    {
        // Arrange: A imports B, B imports A
        var aPath = Path.Combine(_tempDir, "a.spy");
        var bPath = Path.Combine(_tempDir, "b.spy");

        File.WriteAllText(aPath, @"
from b import func_b

def func_a() -> str:
    return ""A""
");

        File.WriteAllText(bPath, @"
from a import func_a

def func_b() -> str:
    return ""B""
");

        var compiler = new Compiler();

        // Act
        var result = compiler.Compile(File.ReadAllText(aPath), aPath);

        // Assert
        Assert.False(result.Success, "Compilation should fail for circular imports");
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("Circular import", StringComparison.OrdinalIgnoreCase));
    }
}
