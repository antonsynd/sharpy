using System;
using System.IO;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Xunit;
using FluentAssertions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for PackageResolver - validates __init__.spy package initialization and re-exports
/// </summary>
public class PackageResolverTests : IDisposable
{
    private readonly string _testDir;
    private readonly ICompilerLogger _logger;

    public PackageResolverTests()
    {
        _logger = NullLogger.Instance;
        _testDir = Path.Combine(Path.GetTempPath(), $"PackageResolverTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region Basic Package Resolution

    [Fact]
    public void ResolvePackage_EmptyInit_ReturnsEmptyPackage()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "empty_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, "# Empty package\n");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("empty_pkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.Name.Should().Be("empty_pkg");
        packageInfo.InitPath.Should().Be(initPath);
        packageInfo.ExportedSymbols.Should().BeEmpty();
    }

    [Fact]
    public void ResolvePackage_WithDirectFunctions_ExportsAll()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "func_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
def public_func() -> None:
    pass

def _protected_func() -> None:
    pass

def __private_func() -> None:
    pass
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("func_pkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().HaveCount(3);
        packageInfo.ExportedSymbols.Should().ContainKey("public_func");
        packageInfo.ExportedSymbols.Should().ContainKey("_protected_func");
        packageInfo.ExportedSymbols.Should().ContainKey("__private_func");

        // Check access levels
        packageInfo.ExportedSymbols["public_func"].AccessLevel.Should().Be(AccessLevel.Public);
        packageInfo.ExportedSymbols["_protected_func"].AccessLevel.Should().Be(AccessLevel.Protected);
        packageInfo.ExportedSymbols["__private_func"].AccessLevel.Should().Be(AccessLevel.Private);
    }

    [Fact]
    public void ResolvePackage_WithClasses_ExportsAll()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "class_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
class MyClass:
    def __init__(self):
        pass

class _InternalClass:
    pass
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("class_pkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().HaveCount(2);
        packageInfo.ExportedSymbols.Should().ContainKey("MyClass");
        packageInfo.ExportedSymbols.Should().ContainKey("_InternalClass");

        var myClass = packageInfo.ExportedSymbols["MyClass"];
        myClass.Should().BeOfType<TypeSymbol>();
        ((TypeSymbol)myClass).TypeKind.Should().Be(TypeKind.Class);
    }

    [Fact]
    public void ResolvePackage_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var resolver = new PackageResolver(_logger);
        var nonExistentPath = Path.Combine(_testDir, "nonexistent", "__init__.spy");

        // Act
        var packageInfo = resolver.ResolvePackage("nonexistent", nonExistentPath);

        // Assert
        packageInfo.Should().BeNull();
    }

    #endregion

    #region Re-exports from "from X import Y"

    [Fact]
    public void ResolvePackage_FromImport_ReExportsSymbols()
    {
        // Arrange
        // Create a module to import from
        var utilsDir = Path.Combine(_testDir, "utils");
        Directory.CreateDirectory(utilsDir);
        var helpersPath = Path.Combine(utilsDir, "helpers.spy");
        File.WriteAllText(helpersPath, @"
def format_string(s: str) -> str:
    return s

def parse_input(s: str) -> int:
    return 0
");

        // Create package that re-exports
        var packageDir = Path.Combine(_testDir, "mypkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
from utils.helpers import format_string, parse_input
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("mypkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().HaveCount(2);
        packageInfo.ExportedSymbols.Should().ContainKey("format_string");
        packageInfo.ExportedSymbols.Should().ContainKey("parse_input");
    }

    [Fact]
    public void ResolvePackage_FromImportWithAlias_ReExportsWithAlias()
    {
        // Arrange
        var utilsDir = Path.Combine(_testDir, "utils");
        Directory.CreateDirectory(utilsDir);
        var mathPath = Path.Combine(utilsDir, "math.spy");
        File.WriteAllText(mathPath, @"
def calculate(x: int) -> int:
    return x * 2
");

        var packageDir = Path.Combine(_testDir, "mypkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
from utils.math import calculate as compute
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("mypkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().ContainKey("compute");
        packageInfo.ExportedSymbols.Should().NotContainKey("calculate");
    }

    [Fact]
    public void ResolvePackage_FromImportStar_ReExportsPublicOnly()
    {
        // Arrange
        var utilsDir = Path.Combine(_testDir, "utils");
        Directory.CreateDirectory(utilsDir);
        var corePath = Path.Combine(utilsDir, "core.spy");
        File.WriteAllText(corePath, @"
def public_func() -> None:
    pass

def _protected_func() -> None:
    pass

def __private_func() -> None:
    pass
");

        var packageDir = Path.Combine(_testDir, "mypkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
from utils.core import *
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("mypkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().ContainKey("public_func");
        packageInfo.ExportedSymbols.Should().NotContainKey("_protected_func");
        packageInfo.ExportedSymbols.Should().NotContainKey("__private_func");
    }

    #endregion

    #region Mixed Direct and Re-exported Symbols

    [Fact]
    public void ResolvePackage_MixedDirectAndImported_ExportsBoth()
    {
        // Arrange
        var utilsDir = Path.Combine(_testDir, "utils");
        Directory.CreateDirectory(utilsDir);
        var helpersPath = Path.Combine(utilsDir, "helpers.spy");
        File.WriteAllText(helpersPath, @"
def external_func() -> None:
    pass
");

        var packageDir = Path.Combine(_testDir, "mypkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
from utils.helpers import external_func

def local_func() -> None:
    pass

class LocalClass:
    pass
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("mypkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().HaveCount(3);
        packageInfo.ExportedSymbols.Should().ContainKey("external_func");
        packageInfo.ExportedSymbols.Should().ContainKey("local_func");
        packageInfo.ExportedSymbols.Should().ContainKey("LocalClass");
    }

    [Fact]
    public void ResolvePackage_LocalOverrideImported_PrefersLocal()
    {
        // Arrange
        var utilsDir = Path.Combine(_testDir, "utils");
        Directory.CreateDirectory(utilsDir);
        var helpersPath = Path.Combine(utilsDir, "helpers.spy");
        File.WriteAllText(helpersPath, @"
def shared_func() -> None:
    pass
");

        var packageDir = Path.Combine(_testDir, "mypkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
# Import first
from utils.helpers import shared_func

# Then define locally (should take precedence since added first in our implementation)
def local_func() -> None:
    pass
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("mypkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().HaveCount(2);
        packageInfo.ExportedSymbols.Should().ContainKey("shared_func");
        packageInfo.ExportedSymbols.Should().ContainKey("local_func");
    }

    #endregion

    #region Caching

    [Fact]
    public void ResolvePackage_SamePackageTwice_UsesCachedResult()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "cached_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
def some_func() -> None:
    pass
");

        var resolver = new PackageResolver(_logger);

        // Act
        var result1 = resolver.ResolvePackage("cached_pkg", initPath);
        var result2 = resolver.ResolvePackage("cached_pkg", initPath);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().BeSameAs(result2); // Same object reference
    }

    [Fact]
    public void ClearCache_RemovesCachedPackages()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "cache_clear_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
def func1() -> None:
    pass
");

        var resolver = new PackageResolver(_logger);

        // Act
        var result1 = resolver.ResolvePackage("cache_clear_pkg", initPath);
        resolver.ClearCache();
        var result2 = resolver.ResolvePackage("cache_clear_pkg", initPath);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().NotBeSameAs(result2); // Different object after cache clear
    }

    #endregion

    #region Error Handling

    [Fact]
    public void ResolvePackage_InvalidSyntax_ReturnsNull()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "invalid_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
def invalid_syntax(
    # Missing closing paren and body
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("invalid_pkg", initPath);

        // Assert
        packageInfo.Should().BeNull();
    }

    [Fact]
    public void ResolvePackage_ImportNonExistentModule_ContinuesProcessing()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "partial_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
from nonexistent.module import something

def local_func() -> None:
    pass
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("partial_pkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        // Should still have the local function
        packageInfo!.ExportedSymbols.Should().ContainKey("local_func");
        // But not the failed import
        packageInfo.ExportedSymbols.Should().NotContainKey("something");
    }

    #endregion

    #region Type Support

    [Fact]
    public void ResolvePackage_WithStructs_ExportsStructs()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "struct_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
struct Point:
    x: int
    y: int
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("struct_pkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().ContainKey("Point");
        var pointSymbol = packageInfo.ExportedSymbols["Point"];
        pointSymbol.Should().BeOfType<TypeSymbol>();
        ((TypeSymbol)pointSymbol).TypeKind.Should().Be(TypeKind.Struct);
    }

    [Fact]
    public void ResolvePackage_WithInterfaces_ExportsInterfaces()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "interface_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
interface IDrawable:
    def draw(self) -> None:
        ...
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("interface_pkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().ContainKey("IDrawable");
        var drawableSymbol = packageInfo.ExportedSymbols["IDrawable"];
        drawableSymbol.Should().BeOfType<TypeSymbol>();
        ((TypeSymbol)drawableSymbol).TypeKind.Should().Be(TypeKind.Interface);
    }

    [Fact]
    public void ResolvePackage_WithEnums_ExportsEnums()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "enum_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("enum_pkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().ContainKey("Color");
        var colorSymbol = packageInfo.ExportedSymbols["Color"];
        colorSymbol.Should().BeOfType<TypeSymbol>();
        ((TypeSymbol)colorSymbol).TypeKind.Should().Be(TypeKind.Enum);
    }

    [Fact]
    public void ResolvePackage_WithConstants_ExportsConstants()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "const_pkg");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, @"
const MAX_SIZE: int = 100
const DEFAULT_NAME: str = 'default'
");

        var resolver = new PackageResolver(_logger);

        // Act
        var packageInfo = resolver.ResolvePackage("const_pkg", initPath);

        // Assert
        packageInfo.Should().NotBeNull();
        packageInfo!.ExportedSymbols.Should().HaveCount(2);
        packageInfo.ExportedSymbols.Should().ContainKey("MAX_SIZE");
        packageInfo.ExportedSymbols.Should().ContainKey("DEFAULT_NAME");

        var maxSize = packageInfo.ExportedSymbols["MAX_SIZE"];
        maxSize.Should().BeOfType<VariableSymbol>();
        ((VariableSymbol)maxSize).IsConstant.Should().BeTrue();
    }

    #endregion
}
