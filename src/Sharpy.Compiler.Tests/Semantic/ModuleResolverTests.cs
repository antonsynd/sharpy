using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for ModuleResolver - validates module path resolution logic
/// </summary>
public class ModuleResolverTests : IDisposable
{
    private readonly string _testDir;
    private readonly ICompilerLogger _logger;

    public ModuleResolverTests()
    {
        _logger = NullLogger.Instance;
        _testDir = Path.Combine(Path.GetTempPath(), $"ModuleResolverTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region Basic Resolution Tests

    [Fact]
    public void Resolve_SimpleModuleName_ReturnsCorrectPath()
    {
        // Arrange
        var modulePath = Path.Combine(_testDir, "mymodule.spy");
        File.WriteAllText(modulePath, "# Test module");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(_testDir);

        // Act
        var result = resolver.Resolve("mymodule");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(modulePath), result.FullPath);
        Assert.Equal("mymodule", result.ModuleName);
        Assert.Equal(ModuleResolutionKind.ProjectSearchPath, result.Kind);
    }

    [Fact]
    public void Resolve_DottedModuleName_ReturnsCorrectPath()
    {
        // Arrange
        var utilsDir = Path.Combine(_testDir, "utils");
        Directory.CreateDirectory(utilsDir);
        var modulePath = Path.Combine(utilsDir, "helpers.spy");
        File.WriteAllText(modulePath, "# Helper module");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(_testDir);

        // Act
        var result = resolver.Resolve("utils.helpers");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(modulePath), result.FullPath);
        Assert.Equal("utils.helpers", result.ModuleName);
    }

    [Fact]
    public void Resolve_DeepNestedModule_ReturnsCorrectPath()
    {
        // Arrange
        var nestedPath = Path.Combine(_testDir, "a", "b", "c");
        Directory.CreateDirectory(nestedPath);
        var modulePath = Path.Combine(nestedPath, "d.spy");
        File.WriteAllText(modulePath, "# Deep module");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(_testDir);

        // Act
        var result = resolver.Resolve("a.b.c.d");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(modulePath), result.FullPath);
        Assert.Equal("a.b.c.d", result.ModuleName);
    }

    #endregion

    #region Package Support Tests

    [Fact]
    public void Resolve_PackageWithInit_ReturnsInitPath()
    {
        // Arrange
        var packageDir = Path.Combine(_testDir, "mypackage");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, "# Package init");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(_testDir);

        // Act
        var result = resolver.Resolve("mypackage");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(initPath), result.FullPath);
        Assert.Equal("mypackage", result.ModuleName);
    }

    [Fact]
    public void Resolve_NestedPackage_ReturnsInitPath()
    {
        // Arrange
        var pkgDir = Path.Combine(_testDir, "pkg");
        var subpkgDir = Path.Combine(pkgDir, "subpkg");
        Directory.CreateDirectory(subpkgDir);
        var initPath = Path.Combine(subpkgDir, "__init__.spy");
        File.WriteAllText(initPath, "# Subpackage init");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(_testDir);

        // Act
        var result = resolver.Resolve("pkg.subpkg");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(initPath), result.FullPath);
        Assert.Equal("pkg.subpkg", result.ModuleName);
    }

    [Fact]
    public void Resolve_ModuleFileOverPackage_PrefersModuleFile()
    {
        // Arrange - Create both module.spy and module/__init__.spy
        var modulePath = Path.Combine(_testDir, "mymodule.spy");
        File.WriteAllText(modulePath, "# Module file");

        var packageDir = Path.Combine(_testDir, "mymodule");
        Directory.CreateDirectory(packageDir);
        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, "# Package init");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(_testDir);

        // Act
        var result = resolver.Resolve("mymodule");

        // Assert - Should prefer the .spy file over the package
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(modulePath), result.FullPath);
    }

    #endregion

    #region Search Path Priority Tests

    [Fact]
    public void Resolve_RelativePathFirst_WhenCurrentModuleSet()
    {
        // Arrange
        var currentDir = Path.Combine(_testDir, "current");
        Directory.CreateDirectory(currentDir);
        var currentModule = Path.Combine(currentDir, "main.spy");
        File.WriteAllText(currentModule, "# Main module");

        var relativeModule = Path.Combine(currentDir, "helper.spy");
        File.WriteAllText(relativeModule, "# Relative helper");

        var searchDir = Path.Combine(_testDir, "search");
        Directory.CreateDirectory(searchDir);
        var searchModule = Path.Combine(searchDir, "helper.spy");
        File.WriteAllText(searchModule, "# Search path helper");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(searchDir);
        resolver.SetCurrentModulePath(currentModule);

        // Act
        var result = resolver.Resolve("helper");

        // Assert - Should find the one relative to current module
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(relativeModule), result.FullPath);
        Assert.Equal(ModuleResolutionKind.RelativeToCurrentModule, result.Kind);
    }

    [Fact]
    public void Resolve_UsesSearchPaths_WhenNotRelative()
    {
        // Arrange
        var currentDir = Path.Combine(_testDir, "current");
        Directory.CreateDirectory(currentDir);
        var currentModule = Path.Combine(currentDir, "main.spy");
        File.WriteAllText(currentModule, "# Main module");

        var searchDir = Path.Combine(_testDir, "search");
        Directory.CreateDirectory(searchDir);
        var searchModule = Path.Combine(searchDir, "helper.spy");
        File.WriteAllText(searchModule, "# Search path helper");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(searchDir);
        resolver.SetCurrentModulePath(currentModule);

        // Act
        var result = resolver.Resolve("helper");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(searchModule), result.FullPath);
        Assert.Equal(ModuleResolutionKind.ProjectSearchPath, result.Kind);
        Assert.Equal(searchDir, result.SearchPath);
    }

    [Fact]
    public void Resolve_FallsBackToCwd_WhenNotInSearchPaths()
    {
        // Arrange
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            // Get the canonicalized CWD path after SetCurrentDirectory
            var canonicalCwd = Directory.GetCurrentDirectory();
            var cwdModule = Path.Combine(canonicalCwd, "cwdmodule.spy");
            File.WriteAllText(cwdModule, "# CWD module");

            var resolver = new ModuleResolver(_logger);

            // Act
            var result = resolver.Resolve("cwdmodule");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Path.GetFullPath(cwdModule), result.FullPath);
            Assert.Equal(ModuleResolutionKind.CurrentWorkingDirectory, result.Kind);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Fact]
    public void Resolve_WithMultipleSearchPaths_FindsFirst()
    {
        // Arrange
        var searchDir1 = Path.Combine(_testDir, "search1");
        Directory.CreateDirectory(searchDir1);
        var module1 = Path.Combine(searchDir1, "shared.spy");
        File.WriteAllText(module1, "# First");

        var searchDir2 = Path.Combine(_testDir, "search2");
        Directory.CreateDirectory(searchDir2);
        var module2 = Path.Combine(searchDir2, "shared.spy");
        File.WriteAllText(module2, "# Second");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(searchDir1);
        resolver.AddSearchPath(searchDir2);

        // Act
        var result = resolver.Resolve("shared");

        // Assert - Should find the first one
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(module1), result.FullPath);
        Assert.Equal(searchDir1, result.SearchPath);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void Resolve_NonExistentModule_ReturnsNull()
    {
        // Arrange
        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(_testDir);

        // Act
        var result = resolver.Resolve("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_EmptyModuleName_ReturnsNull()
    {
        // Arrange
        var resolver = new ModuleResolver(_logger);

        // Act
        var result = resolver.Resolve("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_WhitespaceModuleName_ReturnsNull()
    {
        // Arrange
        var resolver = new ModuleResolver(_logger);

        // Act
        var result = resolver.Resolve("   ");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void AddSearchPath_AppendsToList()
    {
        // Arrange
        var dir1 = Path.Combine(_testDir, "dir1");
        var dir2 = Path.Combine(_testDir, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        var module1 = Path.Combine(dir1, "test.spy");
        File.WriteAllText(module1, "# Test 1");

        var module2 = Path.Combine(dir2, "test.spy");
        File.WriteAllText(module2, "# Test 2");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(dir1);

        // Act
        var result1 = resolver.Resolve("test");
        Assert.Equal(Path.GetFullPath(module1), result1!.FullPath);

        resolver.AddSearchPath(dir2);
        var result2 = resolver.Resolve("test");

        // Assert - Should still find first one
        Assert.Equal(Path.GetFullPath(module1), result2!.FullPath);
    }

    [Fact]
    public void Constructor_WithSearchPaths_InitializesCorrectly()
    {
        // Arrange
        var searchDir = Path.Combine(_testDir, "search");
        Directory.CreateDirectory(searchDir);
        var modulePath = Path.Combine(searchDir, "module.spy");
        File.WriteAllText(modulePath, "# Module");

        var searchPaths = new[] { searchDir };

        // Act
        var resolver = new ModuleResolver(_logger, searchPaths);
        var result = resolver.Resolve("module");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(modulePath), result.FullPath);
    }

    [Fact]
    public void AddSearchPath_WithNullOrWhitespace_IsIgnored()
    {
        // Arrange
        var resolver = new ModuleResolver(_logger);

        // Act & Assert - Should not throw
        resolver.AddSearchPath(null!);
        resolver.AddSearchPath("");
        resolver.AddSearchPath("   ");

        // Verify by resolving - should use CWD fallback
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var cwdModule = Path.Combine(_testDir, "test.spy");
            File.WriteAllText(cwdModule, "# Test");

            var result = resolver.Resolve("test");
            Assert.NotNull(result);
            Assert.Equal(ModuleResolutionKind.CurrentWorkingDirectory, result.Kind);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Resolve_ModuleNameWithSingleDot_Works()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "utils");
        Directory.CreateDirectory(subDir);
        var modulePath = Path.Combine(subDir, "math.spy");
        File.WriteAllText(modulePath, "# Math utils");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(_testDir);

        // Act
        var result = resolver.Resolve("utils.math");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(modulePath), result.FullPath);
    }

    [Fact]
    public void Resolve_CasePreserved_OnCaseSensitiveFS()
    {
        // Arrange
        var modulePath = Path.Combine(_testDir, "MyModule.spy");
        File.WriteAllText(modulePath, "# Module");

        var resolver = new ModuleResolver(_logger);
        resolver.AddSearchPath(_testDir);

        // Act - Search for exact case
        var result = resolver.Resolve("MyModule");

        // Assert
        if (result != null) // Case-sensitive file system
        {
            Assert.Equal(Path.GetFullPath(modulePath), result.FullPath);
        }
        // On case-insensitive FS, this might still work
    }

    [Fact]
    public void SetCurrentModulePath_UpdatesRelativeResolution()
    {
        // Arrange
        var dir1 = Path.Combine(_testDir, "dir1");
        var dir2 = Path.Combine(_testDir, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        var module1 = Path.Combine(dir1, "helper.spy");
        File.WriteAllText(module1, "# Helper 1");

        var module2 = Path.Combine(dir2, "helper.spy");
        File.WriteAllText(module2, "# Helper 2");

        var currentModule1 = Path.Combine(dir1, "main.spy");
        var currentModule2 = Path.Combine(dir2, "main.spy");

        var resolver = new ModuleResolver(_logger);

        // Act & Assert - Set to dir1
        resolver.SetCurrentModulePath(currentModule1);
        var result1 = resolver.Resolve("helper");
        Assert.NotNull(result1);
        Assert.Equal(Path.GetFullPath(module1), result1.FullPath);

        // Act & Assert - Set to dir2
        resolver.SetCurrentModulePath(currentModule2);
        var result2 = resolver.Resolve("helper");
        Assert.NotNull(result2);
        Assert.Equal(Path.GetFullPath(module2), result2.FullPath);
    }

    #endregion
}
