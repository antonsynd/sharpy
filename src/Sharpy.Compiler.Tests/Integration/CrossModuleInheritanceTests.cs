using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Model;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Tests for cross-module inheritance and interface implementation.
/// These tests verify that classes can inherit from classes/interfaces
/// defined in other modules.
/// </summary>
public class CrossModuleInheritanceTests : IntegrationTestBase
{
    public CrossModuleInheritanceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ClassInheritance_FromImportedModule_ResolvesCorrectly()
    {
        // base.spy defines Animal class
        // derived.spy does: from base import Animal; class Dog(Animal): ...
        using var tempDir = new TempDirectory();

        tempDir.CreateFile("base.spy", @"
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    @virtual
    def speak(self) -> str:
        return ""...""
");

        tempDir.CreateFile("derived.spy", @"
from base import Animal

class Dog(Animal):
    breed: str

    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed

    @override
    def speak(self) -> str:
        return ""Woof!""
");

        tempDir.CreateFile("main.spy", @"
from derived import Dog

def main() -> None:
    dog = Dog(""Rex"", ""German Shepherd"")
    print(dog.speak())
");

        var result = CompileMultiFile(tempDir.Path, "main.spy");

        Assert.Empty(result.Errors);
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
        // Dog should inherit from Animal (may use fully qualified name like Sharpy.Test.Base.Exports.Animal)
        var derivedCode = result.GeneratedCSharp["derived.cs"];
        Assert.Contains("class Dog :", derivedCode);
        Assert.Contains("Animal", derivedCode);
    }

    [Fact]
    public void InterfaceImplementation_FromImportedModule_ResolvesCorrectly()
    {
        using var tempDir = new TempDirectory();

        tempDir.CreateFile("shapes.spy", @"
interface IDrawable:
    def draw(self) -> None:
        ...
");

        tempDir.CreateFile("circle.spy", @"
from shapes import IDrawable

class Circle(IDrawable):
    radius: float

    def __init__(self, radius: float):
        self.radius = radius

    def draw(self) -> None:
        pass
");

        tempDir.CreateFile("main.spy", @"
from circle import Circle

def main() -> None:
    c = Circle(5.0)
    c.draw()
");

        var result = CompileMultiFile(tempDir.Path, "main.spy");

        Assert.Empty(result.Errors);
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
        // Circle should implement IDrawable (may use fully qualified name)
        var circleCode = result.GeneratedCSharp["circle.cs"];
        Assert.Contains("class Circle :", circleCode);
        Assert.Contains("IDrawable", circleCode);
    }

    [Fact]
    public void AbstractClass_FromImportedModule_ResolvesCorrectly()
    {
        using var tempDir = new TempDirectory();

        tempDir.CreateFile("geometry.spy", @"
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        ...
");

        tempDir.CreateFile("rectangle.spy", @"
from geometry import Shape

class Rectangle(Shape):
    width: float
    height: float

    def __init__(self, width: float, height: float):
        self.width = width
        self.height = height

    @override
    def area(self) -> float:
        return self.width * self.height
");

        tempDir.CreateFile("main.spy", @"
from rectangle import Rectangle

def main() -> None:
    rect = Rectangle(3.0, 4.0)
    print(rect.area())
");

        var result = CompileMultiFile(tempDir.Path, "main.spy");

        Assert.Empty(result.Errors);
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void MultipleInheritance_InterfacesFromDifferentModules_Works()
    {
        using var tempDir = new TempDirectory();

        tempDir.CreateFile("drawing.spy", @"
interface IDrawable:
    def draw(self) -> None:
        ...
");

        tempDir.CreateFile("persistence.spy", @"
interface ISerializable:
    def serialize(self) -> str:
        ...
");

        tempDir.CreateFile("widget.spy", @"
from drawing import IDrawable
from persistence import ISerializable

class Widget(IDrawable, ISerializable):
    def draw(self) -> None:
        pass

    def serialize(self) -> str:
        return ""{}""
");

        tempDir.CreateFile("main.spy", @"
from widget import Widget

def main() -> None:
    w = Widget()
    w.draw()
    print(w.serialize())
");

        var result = CompileMultiFile(tempDir.Path, "main.spy");

        Assert.Empty(result.Errors);
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
        // Check that Widget implements both interfaces (order may vary)
        var widgetCode = result.GeneratedCSharp["widget.cs"];
        Assert.Contains("IDrawable", widgetCode);
        Assert.Contains("ISerializable", widgetCode);
    }

    [Fact]
    public void ChainedInheritance_AcrossThreeModules_Works()
    {
        using var tempDir = new TempDirectory();

        tempDir.CreateFile("family.spy", @"
class Grandparent:
    @virtual
    def greet(self) -> str:
        return ""Hello from Grandparent""
");

        tempDir.CreateFile("middle.spy", @"
from family import Grandparent

class Parent(Grandparent):
    @override
    def greet(self) -> str:
        return ""Hello from Parent""
");

        tempDir.CreateFile("child.spy", @"
from middle import Parent

class Child(Parent):
    @override
    def greet(self) -> str:
        return ""Hello from Child""
");

        tempDir.CreateFile("main.spy", @"
from child import Child

def main() -> None:
    c = Child()
    print(c.greet())
");

        var result = CompileMultiFile(tempDir.Path, "main.spy");

        Assert.Empty(result.Errors);
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ClassInheritingFromClassAndInterface_Works()
    {
        using var tempDir = new TempDirectory();

        tempDir.CreateFile("base_types.spy", @"
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

interface ISwimmable:
    def swim(self) -> None:
        ...
");

        tempDir.CreateFile("fish.spy", @"
from base_types import Animal, ISwimmable

class Fish(Animal, ISwimmable):
    def __init__(self, name: str):
        super().__init__(name)

    def swim(self) -> None:
        pass
");

        tempDir.CreateFile("main.spy", @"
from fish import Fish

def main() -> None:
    f = Fish(""Nemo"")
    f.swim()
");

        var result = CompileMultiFile(tempDir.Path, "main.spy");

        Assert.Empty(result.Errors);
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ClassInheritance_FromNetBaseClass_Works()
    {
        // Test inheriting from .NET base classes (e.g., System.Exception)
        // This test demonstrates that .NET base class inheritance does NOT work yet.
        // Error: "Base type 'Exception' not found" - the ImportResolver doesn't
        // register .NET types for inheritance resolution.
        var source = @"
from system import Exception

class CustomError(Exception):
    code: int

    def __init__(self, message: str, code: int):
        super().__init__(message)
        self.code = code
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // The generated C# should have CustomError inheriting from Exception
        // (may be fully qualified as System.Exception or just Exception with a using statement)
        Assert.Contains("class CustomError", result.GeneratedCSharp ?? "");
        Assert.Contains(": Exception", result.GeneratedCSharp ?? "");
    }

    /// <summary>
    /// Helper method to compile a multi-file Sharpy project.
    /// </summary>
    private MultiFileCompilationResult CompileMultiFile(string projectDir, string entryPointFile)
    {
        var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.TopDirectoryOnly)
            .ToList();

        var config = new ProjectConfig
        {
            ProjectDirectory = projectDir,
            ProjectFilePath = Path.Combine(projectDir, "test.spyproj"),
            RootNamespace = "Sharpy.Test",
            OutputType = "exe",
            EntryPoint = entryPointFile,
            SourceFiles = sourceFiles,
            Configuration = "Debug",
            TargetFramework = "net10.0"
        };

        var projectCompiler = new ProjectCompiler();
        var result = projectCompiler.Compile(config);

        // Convert to MultiFileCompilationResult format
        var generatedCSharp = new Dictionary<string, string>();
        foreach (var (fileName, code) in result.GeneratedCSharpFiles)
        {
            generatedCSharp[fileName] = code;
        }

        return new MultiFileCompilationResult
        {
            Success = result.Success,
            Errors = result.Errors,
            GeneratedCSharp = generatedCSharp
        };
    }
}

/// <summary>
/// Result of a multi-file compilation.
/// </summary>
public class MultiFileCompilationResult
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, string> GeneratedCSharp { get; set; } = new();
}

