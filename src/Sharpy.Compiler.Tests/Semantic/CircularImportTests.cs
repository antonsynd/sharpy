using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for circular import detection in ImportResolver
/// </summary>
public class CircularImportTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ImportResolver _resolver;

    public CircularImportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _resolver = new ImportResolver(NullLogger.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void CircularImport_Direct_ReportsError()
    {
        // Create two files that import each other
        var fileA = Path.Combine(_tempDir, "a.spy");
        var fileB = Path.Combine(_tempDir, "b.spy");

        File.WriteAllText(fileA, @"
from b import ClassB

class ClassA:
    pass
");

        File.WriteAllText(fileB, @"
from a import ClassA

class ClassB:
    pass
");

        _resolver.SetCurrentModule(fileA);

        // Parse and try to resolve imports in file A
        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } },
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir);

        // Should detect circular import
        Assert.NotEmpty(_resolver.Errors);
        Assert.Contains(_resolver.Errors, e => e.Message.Contains("Circular import detected"));
    }

    [Fact]
    public void CircularImport_Transitive_ReportsError()
    {
        // Create A -> B -> C -> A cycle
        var fileA = Path.Combine(_tempDir, "a.spy");
        var fileB = Path.Combine(_tempDir, "b.spy");
        var fileC = Path.Combine(_tempDir, "c.spy");

        File.WriteAllText(fileA, @"
from b import ClassB

class ClassA:
    pass
");

        File.WriteAllText(fileB, @"
from c import ClassC

class ClassB:
    pass
");

        File.WriteAllText(fileC, @"
from a import ClassA

class ClassC:
    pass
");

        _resolver.SetCurrentModule(fileA);

        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } },
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir);

        // Should detect transitive circular import
        Assert.NotEmpty(_resolver.Errors);
        Assert.Contains(_resolver.Errors, e => e.Message.Contains("Circular import detected"));
    }

    [Fact]
    public void CircularImport_SelfImport_ReportsError()
    {
        // Create a file that imports itself
        var fileA = Path.Combine(_tempDir, "a.spy");

        File.WriteAllText(fileA, @"
from a import something

class ClassA:
    pass
");

        _resolver.SetCurrentModule(fileA);

        var importStmt = new FromImportStatement
        {
            Module = "a",
            Names = new List<ImportAlias> { new ImportAlias { Name = "something" } },
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir);

        // Should detect self-import
        Assert.NotEmpty(_resolver.Errors);
        Assert.Contains(_resolver.Errors, e => e.Message.Contains("Circular import detected"));
    }

    [Fact]
    public void CircularImport_ErrorMessage_ContainsChain()
    {
        // Create A -> B -> C -> A cycle
        var fileA = Path.Combine(_tempDir, "a.spy");
        var fileB = Path.Combine(_tempDir, "b.spy");
        var fileC = Path.Combine(_tempDir, "c.spy");

        File.WriteAllText(fileA, @"
from b import ClassB

class ClassA:
    pass
");

        File.WriteAllText(fileB, @"
from c import ClassC

class ClassB:
    pass
");

        File.WriteAllText(fileC, @"
from a import ClassA

class ClassC:
    pass
");

        _resolver.SetCurrentModule(fileA);

        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } },
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir);

        // Error message should contain the full chain
        Assert.NotEmpty(_resolver.Errors);
        var error = _resolver.Errors.First(e => e.Message.Contains("Circular import detected"));
        Assert.Contains("a.spy", error.Message);
        Assert.Contains("b.spy", error.Message);
        Assert.Contains("c.spy", error.Message);
        Assert.Contains("->", error.Message);
        Assert.Contains("(cycle)", error.Message);
    }

    [Fact]
    public void DiamondDependency_NoCircular_Succeeds()
    {
        // Create diamond dependency: A -> B, A -> C, B -> D, C -> D
        var fileA = Path.Combine(_tempDir, "a.spy");
        var fileB = Path.Combine(_tempDir, "b.spy");
        var fileC = Path.Combine(_tempDir, "c.spy");
        var fileD = Path.Combine(_tempDir, "d.spy");

        File.WriteAllText(fileA, @"
from b import ClassB
from c import ClassC

class ClassA:
    pass
");

        File.WriteAllText(fileB, @"
from d import ClassD

class ClassB:
    pass
");

        File.WriteAllText(fileC, @"
from d import ClassD

class ClassC:
    pass
");

        File.WriteAllText(fileD, @"
class ClassD:
    pass
");

        _resolver.SetCurrentModule(fileA);

        // Import B
        var importB = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } },
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var resultB = _resolver.ResolveFromImport(importB, _tempDir);

        // Import C
        var importC = new FromImportStatement
        {
            Module = "c",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassC" } },
            ImportAll = false,
            LineStart = 3,
            ColumnStart = 1
        };

        var resultC = _resolver.ResolveFromImport(importC, _tempDir);

        // Should succeed - no circular import
        var circularErrors = _resolver.Errors.Where(e => e.Message.Contains("Circular import")).ToList();
        Assert.Empty(circularErrors);
    }

    [Fact]
    public void CachedModule_NotReportedAsCircular()
    {
        // Create files where B is imported multiple times but is cached
        var fileA = Path.Combine(_tempDir, "a.spy");
        var fileB = Path.Combine(_tempDir, "b.spy");

        File.WriteAllText(fileA, @"
from b import ClassB

class ClassA:
    pass
");

        File.WriteAllText(fileB, @"
class ClassB:
    pass
");

        _resolver.SetCurrentModule(fileA);

        // Import B first time
        var import1 = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } },
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result1 = _resolver.ResolveFromImport(import1, _tempDir);

        // Import B second time (should use cache)
        var import2 = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } },
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result2 = _resolver.ResolveFromImport(import2, _tempDir);

        // Should not report circular import
        var circularErrors = _resolver.Errors.Where(e => e.Message.Contains("Circular import")).ToList();
        Assert.Empty(circularErrors);
    }

    [Fact]
    public void CircularImport_WithImportStatement_ReportsError()
    {
        // Test with 'import a' style instead of 'from a import X'
        var fileA = Path.Combine(_tempDir, "a.spy");
        var fileB = Path.Combine(_tempDir, "b.spy");

        File.WriteAllText(fileA, @"
import b

class ClassA:
    pass
");

        File.WriteAllText(fileB, @"
import a

class ClassB:
    pass
");

        _resolver.SetCurrentModule(fileA);

        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias> { new ImportAlias { Name = "b" } },
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveImport(importStmt, _tempDir);

        // Should detect circular import
        Assert.NotEmpty(_resolver.Errors);
        Assert.Contains(_resolver.Errors, e => e.Message.Contains("Circular import detected"));
    }

    [Fact]
    public void CircularImport_WithComplexChain_ShowsFullPath()
    {
        // Create A -> B -> C -> D -> A cycle
        var fileA = Path.Combine(_tempDir, "a.spy");
        var fileB = Path.Combine(_tempDir, "b.spy");
        var fileC = Path.Combine(_tempDir, "c.spy");
        var fileD = Path.Combine(_tempDir, "d.spy");

        File.WriteAllText(fileA, @"
from b import ClassB

class ClassA:
    pass
");

        File.WriteAllText(fileB, @"
from c import ClassC

class ClassB:
    pass
");

        File.WriteAllText(fileC, @"
from d import ClassD

class ClassC:
    pass
");

        File.WriteAllText(fileD, @"
from a import ClassA

class ClassD:
    pass
");

        _resolver.SetCurrentModule(fileA);

        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } },
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir);

        // Error should show the full chain
        Assert.NotEmpty(_resolver.Errors);
        var error = _resolver.Errors.First(e => e.Message.Contains("Circular import detected"));

        // Check that all files appear in the error message
        Assert.Contains("a.spy", error.Message);
        Assert.Contains("b.spy", error.Message);
        Assert.Contains("c.spy", error.Message);
        Assert.Contains("d.spy", error.Message);
    }

    [Fact]
    public void NonCircular_LinearChain_Succeeds()
    {
        // Create a linear chain: A -> B -> C -> D (no cycle)
        var fileA = Path.Combine(_tempDir, "a.spy");
        var fileB = Path.Combine(_tempDir, "b.spy");
        var fileC = Path.Combine(_tempDir, "c.spy");
        var fileD = Path.Combine(_tempDir, "d.spy");

        File.WriteAllText(fileA, @"
from b import ClassB

class ClassA:
    pass
");

        File.WriteAllText(fileB, @"
from c import ClassC

class ClassB:
    pass
");

        File.WriteAllText(fileC, @"
from d import ClassD

class ClassC:
    pass
");

        File.WriteAllText(fileD, @"
class ClassD:
    pass
");

        _resolver.SetCurrentModule(fileA);

        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } },
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir);

        // Should succeed - no circular import
        var circularErrors = _resolver.Errors.Where(e => e.Message.Contains("Circular import")).ToList();
        Assert.Empty(circularErrors);
    }
}
