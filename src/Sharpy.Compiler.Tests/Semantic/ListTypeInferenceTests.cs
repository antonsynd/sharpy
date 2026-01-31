using System.Linq;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for list type inference with subclasses.
/// Verifies that the compiler finds the Least Common Ancestor (LCA) when
/// inferring the element type of a list containing objects of different subclass types.
/// </summary>
public class ListTypeInferenceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ITestOutputHelper _output;

    public ListTypeInferenceTests(ITestOutputHelper output)
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
    public void ListOfSiblingSubclasses_InfersCommonBaseType()
    {
        // Arrange: Two sibling classes with a common base
        var sourcePath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(sourcePath, @"
class WorkItem:
    id: int

class Bug(WorkItem):
    severity: str

class Feature(WorkItem):
    priority: int

def main() -> None:
    bug = Bug()
    feature = Feature()
    # Should infer list[WorkItem], not list[object]
    items: list[WorkItem] = [bug, feature]
    print(len(items))
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { sourcePath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        _output.WriteLine($"Errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    [Fact]
    public void ListOfHomogeneousType_InfersExactType()
    {
        // Arrange: List with elements of the same type
        var sourcePath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(sourcePath, @"
class Bug:
    id: int

def main() -> None:
    bug1 = Bug()
    bug2 = Bug()
    # Should infer list[Bug]
    items: list[Bug] = [bug1, bug2]
    print(len(items))
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { sourcePath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        _output.WriteLine($"Errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    [Fact]
    public void ListOfGrandchildClasses_InfersCommonGrandparent()
    {
        // Arrange: Two grandchild classes should find common grandparent
        var sourcePath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(sourcePath, @"
class Entity:
    id: int

class WorkItem(Entity):
    title: str

class Bug(WorkItem):
    severity: str

class Task(WorkItem):
    assignee: str

def main() -> None:
    bug = Bug()
    task = Task()
    # Should infer list[WorkItem]
    items: list[WorkItem] = [bug, task]
    print(len(items))
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { sourcePath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        _output.WriteLine($"Errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    [Fact]
    public void ListOfUnrelatedClasses_InfersObject()
    {
        // Arrange: Two unrelated classes should infer object
        var sourcePath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(sourcePath, @"
class Cat:
    name: str

class Car:
    brand: str

def main() -> None:
    cat = Cat()
    car = Car()
    # Should infer list[object]
    items: list[object] = [cat, car]
    print(len(items))
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { sourcePath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        _output.WriteLine($"Errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    [Fact]
    public void ListOfSingleElement_InfersElementType()
    {
        // Arrange: Single element list
        var sourcePath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(sourcePath, @"
class Bug:
    id: int

def main() -> None:
    bug = Bug()
    # Should infer list[Bug]
    items: list[Bug] = [bug]
    print(len(items))
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { sourcePath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        _output.WriteLine($"Errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    [Fact]
    public void DictWithSiblingValueTypes_InfersCommonBaseValueType()
    {
        // Arrange: Dict with values of sibling types
        var sourcePath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(sourcePath, @"
class WorkItem:
    id: int

class Bug(WorkItem):
    severity: str

class Feature(WorkItem):
    priority: int

def main() -> None:
    bug = Bug()
    feature = Feature()
    # Should infer dict[str, WorkItem]
    items: dict[str, WorkItem] = {""a"": bug, ""b"": feature}
    print(len(items))
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { sourcePath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        _output.WriteLine($"Errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    [Fact]
    public void SetOfSiblingSubclasses_InfersCommonBaseType()
    {
        // Arrange: Set with sibling types
        var sourcePath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(sourcePath, @"
class WorkItem:
    id: int

class Bug(WorkItem):
    severity: str

class Feature(WorkItem):
    priority: int

def main() -> None:
    bug = Bug()
    feature = Feature()
    # Should infer set[WorkItem]
    items: set[WorkItem] = {bug, feature}
    print(len(items))
");

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            RootNamespace = "TestProject",
            SourceFiles = new List<string> { sourcePath }
        };

        var compiler = new ProjectCompiler(NullLogger.Instance);

        // Act
        var result = compiler.Compile(config);

        // Assert
        _output.WriteLine($"Errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }
}
