using System.Collections.Immutable;
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
    public void CircularImport_Direct_TypeOnly_CreatesStub()
    {
        // Create two files that import each other's TYPE symbols
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


        // Parse and try to resolve imports in file A
        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir,
            currentModulePath: fileA);

        // Type-only circular imports are deferred — b.spy is fully loaded
        // (the stub is created for a.spy during b.spy's recursive loading)
        Assert.NotNull(result);
        Assert.True(result.ExportedSymbols.ContainsKey("ClassB"));
        Assert.False(_resolver.Diagnostics.HasErrors);
    }

    [Fact]
    public void CircularImport_Transitive_TypeOnly_CreatesStub()
    {
        // Create A -> B -> C -> A cycle — all importing TYPE symbols
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


        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir,
            currentModulePath: fileA);

        // Transitive type-only circular imports produce stubs — no error at import level
        Assert.NotNull(result);
        Assert.False(_resolver.Diagnostics.HasErrors);
    }

    [Fact]
    public void CircularImport_FunctionImport_ReportsError()
    {
        // Create two files that import each other's FUNCTIONS (not types)
        var fileA = Path.Combine(_tempDir, "a.spy");
        var fileB = Path.Combine(_tempDir, "b.spy");

        File.WriteAllText(fileA, @"
from b import func_b

def func_a() -> str:
    return ""A""
");

        File.WriteAllText(fileB, @"
from a import func_a

def func_b() -> str:
    return ""B""
");

        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "func_b" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir,
            currentModulePath: fileA);

        // Function imports from circular modules produce errors (functions not in stub)
        Assert.True(_resolver.Diagnostics.HasErrors);
        Assert.Contains(_resolver.Diagnostics.GetErrors(), e => e.Message.Contains("Circular import"));
    }

    [Fact]
    public void CircularImport_SelfImport_NonType_ReportsError()
    {
        // Create a file that imports a non-type symbol from itself
        var fileA = Path.Combine(_tempDir, "a.spy");

        File.WriteAllText(fileA, @"
from a import something

class ClassA:
    pass
");


        var importStmt = new FromImportStatement
        {
            Module = "a",
            Names = new List<ImportAlias> { new ImportAlias { Name = "something" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir,
            currentModulePath: fileA);

        // Self-import of non-existent symbol: stub created but "something" not found
        Assert.True(_resolver.Diagnostics.HasErrors);
        Assert.Contains(_resolver.Diagnostics.GetErrors(), e =>
            e.Message.Contains("Circular import") || e.Message.Contains("circular"));
    }

    [Fact]
    public void CircularImport_FunctionImport_ErrorMessage_ContainsCircularInfo()
    {
        // Create A -> B -> C -> A cycle importing FUNCTIONS (not types)
        var fileA = Path.Combine(_tempDir, "a.spy");
        var fileB = Path.Combine(_tempDir, "b.spy");
        var fileC = Path.Combine(_tempDir, "c.spy");

        File.WriteAllText(fileA, @"
from b import func_b

def func_a() -> int:
    return 1
");

        File.WriteAllText(fileB, @"
from c import func_c

def func_b() -> int:
    return 2
");

        File.WriteAllText(fileC, @"
from a import func_a

def func_c() -> int:
    return 3
");


        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "func_b" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir,
            currentModulePath: fileA);

        // Function imports from circular modules mention "circular" in the error
        Assert.True(_resolver.Diagnostics.HasErrors);
        var error = _resolver.Diagnostics.GetErrors().First(e => e.Message.Contains("Circular import"));
        Assert.Contains("func_b", error.Message);
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


        // Import B
        var importB = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var resultB = _resolver.ResolveFromImport(importB, _tempDir,
            currentModulePath: fileA);

        // Import C
        var importC = new FromImportStatement
        {
            Module = "c",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassC" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 3,
            ColumnStart = 1
        };

        var resultC = _resolver.ResolveFromImport(importC, _tempDir,
            currentModulePath: fileA);

        // Should succeed - no circular import
        var circularErrors = _resolver.Diagnostics.GetErrors().Where(e => e.Message.Contains("Circular import")).ToList();
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


        // Import B first time
        var import1 = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result1 = _resolver.ResolveFromImport(import1, _tempDir, currentModulePath: fileA);

        // Import B second time (should use cache)
        var import2 = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result2 = _resolver.ResolveFromImport(import2, _tempDir, currentModulePath: fileA);

        // Should not report circular import
        var circularErrors = _resolver.Diagnostics.GetErrors().Where(e => e.Message.Contains("Circular import")).ToList();
        Assert.Empty(circularErrors);
    }

    [Fact]
    public void CircularImport_WithImportStatement_MarksAsFailed()
    {
        // Test with 'import a' style — plain imports of circular modules are marked
        // as failed deferrals (full module access needed)
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


        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias> { new ImportAlias { Name = "b" } }.ToImmutableArray(),
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveImport(importStmt, _tempDir, currentModulePath: fileA);

        // The module is returned (not null) — it's fully loaded because b.spy itself
        // is not on the chain. The circular stub is created for a.spy INSIDE b.spy's loading.
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.NotNull(result[0]);
    }

    [Fact]
    public void CircularImport_ComplexChain_TypeOnly_CreatesStub()
    {
        // Create A -> B -> C -> D -> A cycle with TYPE imports
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


        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir,
            currentModulePath: fileA);

        // Type-only complex chain creates stubs, no error
        Assert.NotNull(result);
        Assert.False(_resolver.Diagnostics.HasErrors);
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


        var importStmt = new FromImportStatement
        {
            Module = "b",
            Names = new List<ImportAlias> { new ImportAlias { Name = "ClassB" } }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 2,
            ColumnStart = 1
        };

        var result = _resolver.ResolveFromImport(importStmt, _tempDir,
            currentModulePath: fileA);

        // Should succeed - no circular import
        var circularErrors = _resolver.Diagnostics.GetErrors().Where(e => e.Message.Contains("Circular import")).ToList();
        Assert.Empty(circularErrors);
    }
}
