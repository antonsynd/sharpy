using Sharpy.Compiler.Project;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for cross-module inheritance type checking.
/// Verifies that classes inheriting from base classes defined in other modules
/// are correctly resolved after the phase reordering fix.
/// </summary>
public class CrossModuleInheritanceTests : IDisposable
{
    private readonly string _tempDir;

    public CrossModuleInheritanceTests()
    {
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
    public void ClassInheritingFromImportedClass_Compiles()
    {
        // Arrange: Base class in one module
        var baseModulePath = Path.Combine(_tempDir, "base_module.spy");
        File.WriteAllText(baseModulePath, @"
class Content:
    title: str

    def __init__(self, title: str):
        self.title = title
");

        // Child class in another module
        var childModulePath = Path.Combine(_tempDir, "child_module.spy");
        File.WriteAllText(childModulePath, @"
from base_module import Content

class Article(Content):
    author: str

    def __init__(self, title: str, author: str):
        super().__init__(title)
        self.author = author
");

        // Main file using both
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from child_module import Article
from base_module import Content

def process(item: Content) -> None:
    print(item.title)

def main() -> None:
    article = Article(""Test"", ""Author"")
    process(article)
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { baseModulePath, childModulePath, mainPath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void ClassImplementingImportedInterface_Compiles()
    {
        // Arrange: Interface in one module
        var interfaceModulePath = Path.Combine(_tempDir, "interfaces.spy");
        File.WriteAllText(interfaceModulePath, @"
interface IProcessor:
    def process(self) -> str: ...
");

        // Class implementing interface in another module
        var implModulePath = Path.Combine(_tempDir, "impl.spy");
        File.WriteAllText(implModulePath, @"
from interfaces import IProcessor

class DataProcessor(IProcessor):
    def process(self) -> str:
        return ""processed""
");

        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from impl import DataProcessor

def main() -> None:
    processor = DataProcessor()
    print(processor.process())
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { interfaceModulePath, implModulePath, mainPath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void MultiLevelCrossModuleInheritance_Compiles()
    {
        // Arrange: Three-level inheritance across modules
        // Module A: Base class
        var moduleA = Path.Combine(_tempDir, "module_a.spy");
        File.WriteAllText(moduleA, @"
class Animal:
    name: str
");

        // Module B: Middle class inheriting from A
        var moduleB = Path.Combine(_tempDir, "module_b.spy");
        File.WriteAllText(moduleB, @"
from module_a import Animal

class Mammal(Animal):
    warm_blooded: bool = True
");

        // Module C: Leaf class inheriting from B
        var moduleC = Path.Combine(_tempDir, "module_c.spy");
        File.WriteAllText(moduleC, @"
from module_b import Mammal

class Dog(Mammal):
    breed: str
");

        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from module_a import Animal
from module_c import Dog

def greet(animal: Animal) -> None:
    print(animal.name)

def main() -> None:
    dog = Dog()
    greet(dog)
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { moduleA, moduleB, moduleC, mainPath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void TransitiveImports_InheritanceWorksCorrectly()
    {
        // Module A defines Content
        var moduleA = Path.Combine(_tempDir, "data_models.spy");
        File.WriteAllText(moduleA, @"
class Content:
    title: str
");

        // Module B re-exports Content and adds Article
        var moduleB = Path.Combine(_tempDir, "content_types.spy");
        File.WriteAllText(moduleB, @"
from data_models import Content

class Article(Content):
    author: str
");

        // Module C imports from B and uses polymorphism with A's type
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from content_types import Article
from data_models import Content

def process(item: Content) -> None:
    print(item.title)

def main() -> None:
    article = Article()
    article.title = ""Test""
    process(article)
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { moduleA, moduleB, mainPath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Errors)}");
    }
}
