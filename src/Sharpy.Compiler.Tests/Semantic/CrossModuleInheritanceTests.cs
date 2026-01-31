using System.Linq;
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
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
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
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
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
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
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
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    /// <summary>
    /// Tests that use Compiler.Compile() (single-file path with dynamic imports)
    /// to verify that the re-exported type symbol preserves UnresolvedBaseName
    /// and that IsAssignableTo works when Symbol is null.
    /// </summary>
    [Fact]
    public void SingleFileCompile_InheritedMethodAccessible()
    {
        // This tests the Compiler.Compile() path (not ProjectCompiler).
        // Previously failed because CreateReExportedTypeSymbol didn't copy
        // UnresolvedBaseName, so BaseType was never resolved for imported types.
        var basePath = Path.Combine(_tempDir, "animal.spy");
        File.WriteAllText(basePath, @"
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    @virtual
    def speak(self) -> str:
        return ""...""
");

        var derivedPath = Path.Combine(_tempDir, "dog.spy");
        File.WriteAllText(derivedPath, @"
from animal import Animal

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    @override
    def speak(self) -> str:
        return ""Woof!""
");

        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from animal import Animal
from dog import Dog

def main() -> None:
    d: Dog = Dog(""Rex"")
    print(d.speak())
    print(d.name)
");

        // Use Compiler.Compile() directly (the CLI path)
        var options = new CompilerOptions { ModulePaths = new[] { _tempDir } };
        var compiler = new Compiler(options, NullLogger.Instance);
        var source = File.ReadAllText(mainPath);

        var result = compiler.Compile(source, mainPath);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    [Fact]
    public void SingleFileCompile_SubtypePassedToBaseParameter()
    {
        // Tests that a derived type can be passed where base type is expected
        // via the Compiler.Compile() path.
        var basePath = Path.Combine(_tempDir, "shape.spy");
        File.WriteAllText(basePath, @"
class Shape:
    pass

def process_shape(s: Shape) -> str:
    return ""processed""
");

        var derivedPath = Path.Combine(_tempDir, "circle.spy");
        File.WriteAllText(derivedPath, @"
from shape import Shape

class Circle(Shape):
    radius: float

    def __init__(self, r: float):
        self.radius = r
");

        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from shape import Shape, process_shape
from circle import Circle

def main() -> None:
    c: Circle = Circle(5.0)
    result: str = process_shape(c)
    print(result)
");

        var options = new CompilerOptions { ModulePaths = new[] { _tempDir } };
        var compiler = new Compiler(options, NullLogger.Instance);
        var source = File.ReadAllText(mainPath);

        var result = compiler.Compile(source, mainPath);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    /// <summary>
    /// Tests that transitive base types are auto-imported when only the leaf type
    /// is explicitly imported. Entity -> NamedEntity -> User, only User is imported.
    /// </summary>
    [Fact]
    public void SingleFileCompile_TransitiveBaseTypeAutoImported()
    {
        // Entity (root)
        var entityPath = Path.Combine(_tempDir, "entity.spy");
        File.WriteAllText(entityPath, @"
class Entity:
    id: int

    def __init__(self, id: int):
        self.id = id
");

        // NamedEntity extends Entity
        var namedEntityPath = Path.Combine(_tempDir, "named_entity.spy");
        File.WriteAllText(namedEntityPath, @"
from entity import Entity

class NamedEntity(Entity):
    name: str

    def __init__(self, id: int, name: str):
        super().__init__(id)
        self.name = name
");

        // User extends NamedEntity
        var userPath = Path.Combine(_tempDir, "user.spy");
        File.WriteAllText(userPath, @"
from named_entity import NamedEntity

class User(NamedEntity):
    email: str

    def __init__(self, id: int, name: str, email: str):
        super().__init__(id, name)
        self.email = email
");

        // Main only imports User — Entity and NamedEntity should be auto-imported
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from user import User

def main() -> None:
    u: User = User(1, ""Alice"", ""alice@example.com"")
    print(u.id)
    print(u.name)
    print(u.email)
");

        var options = new CompilerOptions { ModulePaths = new[] { _tempDir } };
        var compiler = new Compiler(options, NullLogger.Instance);
        var source = File.ReadAllText(mainPath);

        var result = compiler.Compile(source, mainPath);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    /// <summary>
    /// Tests that transitive interface types are auto-imported when only the
    /// implementing class is explicitly imported.
    /// </summary>
    [Fact]
    public void SingleFileCompile_TransitiveInterfaceAutoImported()
    {
        // Interface in its own module
        var ifacePath = Path.Combine(_tempDir, "printable.spy");
        File.WriteAllText(ifacePath, @"
interface IPrintable:
    def display(self) -> str: ...
");

        // Class implementing the interface
        var implPath = Path.Combine(_tempDir, "widget.spy");
        File.WriteAllText(implPath, @"
from printable import IPrintable

class Widget(IPrintable):
    label: str

    def __init__(self, label: str):
        self.label = label

    def display(self) -> str:
        return self.label
");

        // Main only imports Widget — IPrintable should be auto-imported
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from widget import Widget

def main() -> None:
    w: Widget = Widget(""OK"")
    print(w.display())
");

        var options = new CompilerOptions { ModulePaths = new[] { _tempDir } };
        var compiler = new Compiler(options, NullLogger.Instance);
        var source = File.ReadAllText(mainPath);

        var result = compiler.Compile(source, mainPath);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    /// <summary>
    /// Tests that transitive base types are auto-imported via ProjectCompiler
    /// when the main file only imports the leaf type and the base types are
    /// in other project source files but not explicitly imported by main.
    /// </summary>
    [Fact]
    public void ProjectCompiler_TransitiveBaseTypeResolution()
    {
        // Entity (root)
        var entityPath = Path.Combine(_tempDir, "entity.spy");
        File.WriteAllText(entityPath, @"
class Entity:
    id: int

    def __init__(self, id: int):
        self.id = id
");

        // NamedEntity extends Entity
        var namedEntityPath = Path.Combine(_tempDir, "named_entity.spy");
        File.WriteAllText(namedEntityPath, @"
from entity import Entity

class NamedEntity(Entity):
    name: str

    def __init__(self, id: int, name: str):
        super().__init__(id)
        self.name = name
");

        // User extends NamedEntity
        var userPath = Path.Combine(_tempDir, "user.spy");
        File.WriteAllText(userPath, @"
from named_entity import NamedEntity

class User(NamedEntity):
    email: str

    def __init__(self, id: int, name: str, email: str):
        super().__init__(id, name)
        self.email = email
");

        // Main only imports User — Entity and NamedEntity resolved transitively
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from user import User

def main() -> None:
    u: User = User(1, ""Bob"", ""bob@example.com"")
    print(u.id)
    print(u.name)
    print(u.email)
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { entityPath, namedEntityPath, userPath, mainPath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        var result = compiler.Compile(config);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    [Fact]
    public void SingleFileCompile_ImportedFunctionReturnTypeAssignable()
    {
        // Tests the IsAssignableTo fix: imported function return types have
        // Symbol=null but should still match by name.
        var modulePath = Path.Combine(_tempDir, "validators.spy");
        File.WriteAllText(modulePath, @"
class Result:
    ok: bool
    msg: str

    def __init__(self, ok: bool, msg: str):
        self.ok = ok
        self.msg = msg

def validate(value: int) -> Result:
    if value > 0:
        return Result(True, ""valid"")
    return Result(False, ""invalid"")
");

        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, @"
from validators import Result, validate

def main() -> None:
    r: Result = validate(42)
    print(r.msg)
");

        var options = new CompilerOptions { ModulePaths = new[] { _tempDir } };
        var compiler = new Compiler(options, NullLogger.Instance);
        var source = File.ReadAllText(mainPath);

        var result = compiler.Compile(source, mainPath);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }
}
