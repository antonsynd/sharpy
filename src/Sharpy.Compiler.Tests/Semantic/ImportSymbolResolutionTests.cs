using System.Collections.Immutable;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for import symbol resolution with visibility rules
/// </summary>
public class ImportSymbolResolutionTests : IDisposable
{
    private readonly string _testDir;
    private readonly ICompilerLogger _logger;

    public ImportSymbolResolutionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _logger = NullLogger.Instance;
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region Test Helpers

    private string CreateModuleFile(string moduleName, string content)
    {
        var filePath = Path.Combine(_testDir, $"{moduleName}.spy");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    #endregion

    #region Public Symbol Tests

    [Fact]
    public void FromImport_PublicFunction_Succeeds()
    {
        // Create a module with a public function
        CreateModuleFile("mymodule", @"
def public_func():
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "public_func", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors);
        Assert.Contains("public_func", result.ExportedSymbols.Keys);
        Assert.Equal(AccessLevel.Public, result.ExportedSymbols["public_func"].AccessLevel);
    }

    [Fact]
    public void FromImport_PublicClass_Succeeds()
    {
        CreateModuleFile("mymodule", @"
class PublicClass:
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "PublicClass", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors);
        Assert.Contains("PublicClass", result.ExportedSymbols.Keys);
        Assert.Equal(AccessLevel.Public, result.ExportedSymbols["PublicClass"].AccessLevel);
    }

    #endregion

    #region Protected Symbol Tests (_protected)

    [Fact]
    public void FromImport_ProtectedFunction_Succeeds()
    {
        CreateModuleFile("mymodule", @"
def _protected_func():
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "_protected_func", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors); // Direct import of protected symbols is allowed
        Assert.Contains("_protected_func", result.ExportedSymbols.Keys);
        Assert.Equal(AccessLevel.Protected, result.ExportedSymbols["_protected_func"].AccessLevel);
    }

    [Fact]
    public void FromImportAll_ProtectedFunction_NotIncluded()
    {
        CreateModuleFile("mymodule", @"
def public_func():
    pass

def _protected_func():
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = ImmutableArray<ImportAlias>.Empty,
            ImportAll = true,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors);

        // Get symbols available via import *
        var importAllSymbols = resolver.GetImportAllSymbols(result);

        // public_func should be included
        Assert.Contains("public_func", importAllSymbols.Keys);

        // _protected_func should NOT be included in import *
        Assert.DoesNotContain("_protected_func", importAllSymbols.Keys);

        // But _protected_func should exist in the module's full exported symbols
        Assert.Contains("_protected_func", result.ExportedSymbols.Keys);
    }

    #endregion

    #region Private Symbol Tests (__private)

    [Fact]
    public void FromImport_PrivateFunction_Fails()
    {
        CreateModuleFile("mymodule", @"
def __private_func():
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "__private_func", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.NotEmpty(resolver.Errors);
        Assert.Contains(resolver.Errors, e => e.Message.Contains("private") && e.Message.Contains("__private_func"));
    }

    [Fact]
    public void FromImportAll_PrivateFunction_NotIncluded()
    {
        CreateModuleFile("mymodule", @"
def public_func():
    pass

def __private_func():
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = ImmutableArray<ImportAlias>.Empty,
            ImportAll = true,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors);

        // Get symbols available via import *
        var importAllSymbols = resolver.GetImportAllSymbols(result);

        // public_func should be included
        Assert.Contains("public_func", importAllSymbols.Keys);

        // __private_func should NOT be included in import *
        Assert.DoesNotContain("__private_func", importAllSymbols.Keys);
    }

    #endregion

    #region Mixed Symbol Tests

    [Fact]
    public void FromImport_MixedVisibility_RespectsRules()
    {
        CreateModuleFile("mymodule", @"
def public_func():
    pass

def _protected_func():
    pass

def __private_func():
    pass

class PublicClass:
    pass

class _ProtectedClass:
    pass

class __PrivateClass:
    pass

const MY_CONSTANT: int = 42
const _PROTECTED_CONSTANT: int = 99
const __PRIVATE_CONSTANT: int = 123
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        // Test direct import of public symbol
        var publicImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "public_func", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };
        var publicResult = resolver.ResolveFromImport(publicImport, _testDir);
        Assert.NotNull(publicResult);
        Assert.Empty(resolver.Errors);

        // Test direct import of protected symbol (should succeed)
        var protectedImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "_protected_func", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };
        var protectedResolver = new ImportResolver(_logger);
        protectedResolver.SetCurrentModule(_testDir);
        var protectedResult = protectedResolver.ResolveFromImport(protectedImport, _testDir);
        Assert.NotNull(protectedResult);
        Assert.Empty(protectedResolver.Errors);

        // Test direct import of private symbol (should fail)
        var privateImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "__private_func", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };
        var privateResolver = new ImportResolver(_logger);
        privateResolver.SetCurrentModule(_testDir);
        var privateResult = privateResolver.ResolveFromImport(privateImport, _testDir);
        Assert.NotNull(privateResult);
        Assert.NotEmpty(privateResolver.Errors);
    }

    [Fact]
    public void FromImportAll_OnlyPublicSymbols_Included()
    {
        CreateModuleFile("mymodule", @"
def public_func():
    pass

def _protected_func():
    pass

def __private_func():
    pass

class PublicClass:
    pass

class _ProtectedClass:
    pass

class __PrivateClass:
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = ImmutableArray<ImportAlias>.Empty,
            ImportAll = true,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors);

        // Get symbols available via import *
        var importAllSymbols = resolver.GetImportAllSymbols(result);

        // Only public symbols should be included
        Assert.Contains("public_func", importAllSymbols.Keys);
        Assert.Contains("PublicClass", importAllSymbols.Keys);

        // Protected and private should NOT be in import *
        Assert.DoesNotContain("_protected_func", importAllSymbols.Keys);
        Assert.DoesNotContain("_ProtectedClass", importAllSymbols.Keys);
        Assert.DoesNotContain("__private_func", importAllSymbols.Keys);
        Assert.DoesNotContain("__PrivateClass", importAllSymbols.Keys);

        // But they should exist in the full ExportedSymbols
        Assert.Contains("_protected_func", result.ExportedSymbols.Keys);
        Assert.Contains("__private_func", result.ExportedSymbols.Keys);
    }

    #endregion

    #region Multiple Imports

    [Fact]
    public void FromImport_MultipleSymbols_ValidatesEach()
    {
        CreateModuleFile("mymodule", @"
def public_func():
    pass

def _protected_func():
    pass

def __private_func():
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "public_func", AsName = null, LineStart = 1, ColumnStart = 1 },
                new ImportAlias { Name = "_protected_func", AsName = null, LineStart = 1, ColumnStart = 10 },
                new ImportAlias { Name = "__private_func", AsName = null, LineStart = 1, ColumnStart = 20 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);

        // Should have error only for __private_func
        Assert.Single(resolver.Errors);
        Assert.Contains(resolver.Errors, e => e.Message.Contains("__private_func"));
    }

    #endregion

    #region Non-existent Symbol Tests

    [Fact]
    public void FromImport_NonExistentSymbol_ReportsError()
    {
        CreateModuleFile("mymodule", @"
def existing_func():
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "nonexistent_func", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.NotEmpty(resolver.Errors);
        Assert.Contains(resolver.Errors, e => e.Message.Contains("nonexistent_func"));
    }

    #endregion

    #region Struct, Interface, Enum Tests

    [Fact]
    public void FromImport_ProtectedStruct_Succeeds()
    {
        CreateModuleFile("mymodule", @"
struct _ProtectedStruct:
    value: int
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "_ProtectedStruct", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors);
        Assert.Contains("_ProtectedStruct", result.ExportedSymbols.Keys);
        Assert.Equal(AccessLevel.Protected, result.ExportedSymbols["_ProtectedStruct"].AccessLevel);
    }

    [Fact]
    public void FromImport_PrivateInterface_Fails()
    {
        CreateModuleFile("mymodule", @"
interface __PrivateInterface:
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "__PrivateInterface", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.NotEmpty(resolver.Errors);
        Assert.Contains(resolver.Errors, e => e.Message.Contains("private") && e.Message.Contains("__PrivateInterface"));
    }

    [Fact]
    public void FromImport_PublicEnum_Succeeds()
    {
        CreateModuleFile("mymodule", @"
enum Color:
    Red = 1
    Green = 2
    Blue = 3
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "Color", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors);
        Assert.Contains("Color", result.ExportedSymbols.Keys);
        Assert.Equal(AccessLevel.Public, result.ExportedSymbols["Color"].AccessLevel);
    }

    #endregion

    #region Constant Tests

    [Fact]
    public void FromImport_PublicConstant_Succeeds()
    {
        CreateModuleFile("mymodule", @"
const PI: float = 3.14159
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "PI", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors);
        Assert.Contains("PI", result.ExportedSymbols.Keys);
        Assert.Equal(AccessLevel.Public, result.ExportedSymbols["PI"].AccessLevel);
    }

    [Fact]
    public void FromImport_ProtectedConstant_Succeeds()
    {
        CreateModuleFile("mymodule", @"
const _MAX_SIZE: int = 1000
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "_MAX_SIZE", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors);
        Assert.Contains("_MAX_SIZE", result.ExportedSymbols.Keys);
        Assert.Equal(AccessLevel.Protected, result.ExportedSymbols["_MAX_SIZE"].AccessLevel);
    }

    [Fact]
    public void FromImport_PrivateConstant_Fails()
    {
        CreateModuleFile("mymodule", @"
const __SECRET_KEY: str = ""secret""
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "__SECRET_KEY", AsName = null, LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.NotEmpty(resolver.Errors);
        Assert.Contains(resolver.Errors, e => e.Message.Contains("private") && e.Message.Contains("__SECRET_KEY"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FromImportAll_EmptyModule_NoErrors()
    {
        CreateModuleFile("mymodule", @"
# Empty module
pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = ImmutableArray<ImportAlias>.Empty,
            ImportAll = true,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        Assert.Empty(resolver.Errors);

        var importAllSymbols = resolver.GetImportAllSymbols(result);
        Assert.Empty(importAllSymbols);
    }

    [Fact]
    public void FromImport_WithAlias_PreservesVisibilityChecks()
    {
        CreateModuleFile("mymodule", @"
def _protected_func():
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "_protected_func", AsName = "my_func", LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        // Protected symbols can be directly imported (even with alias)
        Assert.Empty(resolver.Errors);
    }

    [Fact]
    public void FromImport_PrivateSymbolWithAlias_StillFails()
    {
        CreateModuleFile("mymodule", @"
def __private_func():
    pass
");

        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(_testDir);

        var fromImport = new FromImportStatement
        {
            Module = "mymodule",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "__private_func", AsName = "my_func", LineStart = 1, ColumnStart = 1 }
            }.ToImmutableArray(),
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        var result = resolver.ResolveFromImport(fromImport, _testDir);

        Assert.NotNull(result);
        // Private symbols cannot be imported, even with alias
        Assert.NotEmpty(resolver.Errors);
        Assert.Contains(resolver.Errors, e => e.Message.Contains("private") && e.Message.Contains("__private_func"));
    }

    #endregion
}
